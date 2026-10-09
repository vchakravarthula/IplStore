using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;


namespace IplStore.Infrastructure.BackgroundJobs
{
    public class OutboxProcessorBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessorBackgroundService> _logger;

        public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

                    var messages = await dbContext.OutboxMessages
                        .Where(m => m.ProcessedAtUtc == null)
                        .OrderBy(m => m.CreatedAtUtc)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    foreach (var message in messages)
                    {
                        try
                        {
                            await publisher.PublishAsync(message.EventType.ToLowerInvariant(), message.Payload, stoppingToken);
                            message.MarkAsProcessed();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process OutboxMessage ID {Id}", message.Id);
                            message.MarkAsFailed(ex.Message);
                        }
                    }

                    if (messages.Any())
                    {
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Outbox background service execution loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
