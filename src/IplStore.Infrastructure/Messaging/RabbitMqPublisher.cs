using System.Text;
using IplStore.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace IplStore.Infrastructure.Messaging;

/// <summary>
/// Publishes with publisher confirms: PublishAsync returns only after the broker has accepted the message and
/// THROWS otherwise. The outbox relies on that contract: a swallowed failure here would mark the message as sent
/// and lose it.
/// </summary>
public sealed class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly HashSet<string> _declaredQueues = new();
    private readonly object _gate = new();   // IModel is not thread-safe

    public RabbitMqPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = Required(configuration, "RabbitMQ:Host"),
            Port = configuration.GetValue("RabbitMQ:Port", 5672),
            UserName = Required(configuration, "RabbitMQ:Username"),
            Password = Required(configuration, "RabbitMQ:Password"),
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ConfirmSelect();
    }

    public Task PublishAsync(string queueName, string payload, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_connection.IsOpen || _channel.IsClosed)
                throw new InvalidOperationException("RabbitMQ connection is not open.");

            if (!_declaredQueues.Contains(queueName))
            {
                _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
                _declaredQueues.Add(queueName);   // only after a successful declare
            }

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            _channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(payload));

            if (!_channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException($"RabbitMQ did not confirm the message for queue '{queueName}'.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"Configuration value '{key}' is required.");
}
