using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.Messaging;

/// <summary>
/// Relays outbox rows to the broker (at-least-once; consumers must be idempotent on EventId).
/// Safe to run on many replicas: a row is "claimed" with a lease via one conditional UPDATE, so only one replica
/// wins each row. Failed rows back off exponentially and are dead-lettered after OutboxMessage.MaxAttempts.
/// </summary>
public class OutboxProcessor
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);
    private const int BatchSize = 20;

    private readonly IplStoreDbContext _db;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IplStoreDbContext db, IMessagePublisher publisher, ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    /// <returns>Number of messages successfully published in this batch.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var candidateIds = await _db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAtUtc == null
                        && m.DeadLetteredAtUtc == null
                        && (m.LockedUntilUtc == null || m.LockedUntilUtc < now))
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        var published = 0;
        foreach (var id in candidateIds)
        {
            DateTime? leaseUntil = now.Add(Lease);

            // Claim: succeeds for exactly one replica; the loser sees 0 rows affected and skips the message.
            var claimed = await _db.OutboxMessages
                .Where(m => m.Id == id
                            && m.ProcessedAtUtc == null
                            && m.DeadLetteredAtUtc == null
                            && (m.LockedUntilUtc == null || m.LockedUntilUtc < now))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntilUtc, leaseUntil), ct);

            if (claimed == 0) continue;

            var message = await _db.OutboxMessages.FirstAsync(m => m.Id == id, ct);
            try
            {
                await _publisher.PublishAsync(message.EventType.ToLowerInvariant(), message.Payload, ct);
                message.MarkAsProcessed(DateTime.UtcNow);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Publishing outbox message {MessageId} ({EventType}) failed (attempt {Attempt}).",
                    message.Id, message.EventType, message.Attempts + 1);
                message.RegisterFailure(ex.Message, DateTime.UtcNow);

                if (message.DeadLetteredAtUtc is not null)
                    _logger.LogCritical("Outbox message {MessageId} ({EventType}) was dead-lettered after {Attempts} attempts.",
                        message.Id, message.EventType, message.Attempts);
            }

            await _db.SaveChangesAsync(ct);
        }

        return published;
    }
}
