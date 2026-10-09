using IplStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IplStore.Infrastructure.Messaging
{
    public class RabbitMqPublisher : IMessagePublisher, IDisposable
    {
        private RabbitMQ.Client.IConnection? _connection;
        private RabbitMQ.Client.IModel _channel;
        private readonly ConnectionFactory _factory;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public RabbitMqPublisher(IConfiguration configuration)
        {
            _factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:Username"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };

            // Allow exceptions to throw during creation so callers know connection failed
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Enable Publisher Confirms on channel
            _channel.ConfirmSelect();
        }


        public Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
        {
            if (_channel is null || _connection is null || !_connection.IsOpen)
            {
                throw new InvalidOperationException("RabbitMQ connection or channel is not open.");
            }

            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            byte[] body = message switch
            {
                string jsonString => Encoding.UTF8.GetBytes(jsonString),
                _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))
            };

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            // Wait for broker ACK with a 5-second timeout
            bool confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));

            if (!confirmed)
            {
                throw new InvalidOperationException($"RabbitMQ publisher confirmation failed/NACKed for queue '{queueName}'.");
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
