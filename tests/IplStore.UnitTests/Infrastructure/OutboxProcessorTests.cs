using IplStore.Domain.Common;
using IplStore.Infrastructure.Messaging;
using IplStore.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Infrastructure;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeMessagePublisher _publisher = new();

    public void Dispose() => _db.Dispose();

    private OutboxProcessor NewProcessor() =>
        new(_db.CreateContext(), _publisher, NullLogger<OutboxProcessor>.Instance);

    private async Task<Guid> AddMessageAsync(string type = "OrderPlaced", string payload = "{\"OrderId\":1}")
    {
        await using var ctx = _db.CreateContext();
        var message = new OutboxMessage(type, payload);
        ctx.OutboxMessages.Add(message);
        await ctx.SaveChangesAsync();
        return message.Id;
    }

    private async Task<OutboxMessage> LoadAsync(Guid id)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    [Fact]
    public async Task Publishes_the_raw_payload_unmodified_to_a_queue_named_after_the_event_and_marks_it_processed()
    {
        var id = await AddMessageAsync(payload: "{\"OrderId\":42}");

        var published = await NewProcessor().ProcessBatchAsync();

        Assert.Equal(1, published);
        var (queue, payload) = Assert.Single(_publisher.Published);
        Assert.Equal("orderplaced", queue);
        Assert.Equal("{\"OrderId\":42}", payload);   // not re-serialized: no double-encoded JSON
        Assert.NotNull((await LoadAsync(id)).ProcessedAtUtc);
    }

    [Fact]
    public async Task A_broker_failure_never_marks_the_message_processed_so_nothing_is_lost()
    {
        var id = await AddMessageAsync();
        _publisher.Fail = true;

        var published = await NewProcessor().ProcessBatchAsync();

        var message = await LoadAsync(id);
        Assert.Equal(0, published);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, message.Attempts);
        Assert.NotNull(message.LockedUntilUtc);   // backing off before the next try
    }

    [Fact]
    public async Task A_message_in_back_off_is_skipped_and_retried_after_the_lease_expires()
    {
        var id = await AddMessageAsync();
        _publisher.Fail = true;
        await NewProcessor().ProcessBatchAsync();

        _publisher.Fail = false;
        Assert.Equal(0, await NewProcessor().ProcessBatchAsync());   // still locked: another replica (or back-off) owns it

        await using (var ctx = _db.CreateContext())
            await ctx.OutboxMessages.Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntilUtc, (DateTime?)DateTime.UtcNow.AddSeconds(-1)));

        Assert.Equal(1, await NewProcessor().ProcessBatchAsync());
        Assert.NotNull((await LoadAsync(id)).ProcessedAtUtc);
    }

    [Fact]
    public async Task A_poison_message_is_dead_lettered_after_max_attempts_and_stops_being_retried()
    {
        var id = await AddMessageAsync();
        _publisher.Fail = true;

        for (var i = 0; i < OutboxMessage.MaxAttempts; i++)
        {
            await using (var ctx = _db.CreateContext())   // skip the back-off wait
                await ctx.OutboxMessages.Where(m => m.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntilUtc, (DateTime?)null));
            await NewProcessor().ProcessBatchAsync();
        }

        var message = await LoadAsync(id);
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Equal(OutboxMessage.MaxAttempts, message.Attempts);

        _publisher.Fail = false;
        Assert.Equal(0, await NewProcessor().ProcessBatchAsync());   // parked, not retried forever
    }

    [Fact]
    public async Task A_message_leased_by_another_replica_is_not_published_twice()
    {
        var id = await AddMessageAsync();
        await using (var ctx = _db.CreateContext())
            await ctx.OutboxMessages.Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntilUtc, (DateTime?)DateTime.UtcNow.AddSeconds(30)));

        var published = await NewProcessor().ProcessBatchAsync();

        Assert.Equal(0, published);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Already_processed_messages_are_ignored()
    {
        await AddMessageAsync();
        await NewProcessor().ProcessBatchAsync();

        Assert.Equal(0, await NewProcessor().ProcessBatchAsync());
        Assert.Single(_publisher.Published);
    }
}
