namespace IplStore.Domain.Common;

public class OutboxMessage : Entity
{
    public const int MaxAttempts = 5;

    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime? DeadLetteredAtUtc { get; private set; }
    // Lease: while in the future, another replica owns this message. Doubles as the retry back-off.
    public DateTime? LockedUntilUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string eventType, string payload)
    {
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));

        EventType = eventType;
        Payload = payload;
    }

    public void MarkAsProcessed(DateTime nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        LockedUntilUtc = null;
        Error = null;
    }

    public void RegisterFailure(string error, DateTime nowUtc)
    {
        Attempts++;
        Error = error.Length > 1000 ? error[..1000] : error;

        if (Attempts >= MaxAttempts)
        {
            DeadLetteredAtUtc = nowUtc;   // stop retrying; alert on this
            LockedUntilUtc = null;
        }
        else
        {
            LockedUntilUtc = nowUtc.AddSeconds(Math.Min(60, Math.Pow(2, Attempts))); // 2s, 4s, 8s, 16s
        }
    }
}
