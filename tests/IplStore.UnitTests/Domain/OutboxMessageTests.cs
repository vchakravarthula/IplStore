using IplStore.Domain.Common;

namespace IplStore.UnitTests.Domain;

public class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Failure_increments_attempts_and_applies_exponential_backoff()
    {
        var m = new OutboxMessage("OrderPlaced", "{}");

        m.RegisterFailure("boom", Now);
        Assert.Equal(1, m.Attempts);
        Assert.Equal(Now.AddSeconds(2), m.LockedUntilUtc);

        m.RegisterFailure("boom", Now);
        Assert.Equal(Now.AddSeconds(4), m.LockedUntilUtc);
        Assert.Null(m.DeadLetteredAtUtc);
    }

    [Fact]
    public void Message_is_dead_lettered_after_max_attempts_so_a_poison_message_cannot_block_the_queue()
    {
        var m = new OutboxMessage("OrderPlaced", "{}");
        for (var i = 0; i < OutboxMessage.MaxAttempts; i++) m.RegisterFailure("boom", Now);

        Assert.Equal(Now, m.DeadLetteredAtUtc);
        Assert.Null(m.LockedUntilUtc);
    }

    [Fact]
    public void Success_clears_lease_and_error()
    {
        var m = new OutboxMessage("OrderPlaced", "{}");
        m.RegisterFailure("boom", Now);
        m.MarkAsProcessed(Now);

        Assert.Equal(Now, m.ProcessedAtUtc);
        Assert.Null(m.LockedUntilUtc);
        Assert.Null(m.Error);
    }
}
