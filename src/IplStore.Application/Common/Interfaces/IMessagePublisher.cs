namespace IplStore.Application.Common.Interfaces;

public interface IMessagePublisher
{
    /// <summary>Publishes an already-serialized JSON payload. Must THROW if the broker did not confirm the message.</summary>
    Task PublishAsync(string queueName, string payload, CancellationToken ct = default);
}
