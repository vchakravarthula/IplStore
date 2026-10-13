using IplStore.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.BackgroundJobs;

/// <summary>Thin loop around <see cref="OutboxProcessor"/> (all the logic, and the tests, live in the processor).</summary>
public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(IServiceScopeFactory scopes, ILogger<OutboxProcessorBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;
            try
            {
                using var scope = _scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                var published = await processor.ProcessBatchAsync(stoppingToken);
                if (published > 0) delay = TimeSpan.Zero;   // backlog: keep draining without sleeping
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing loop failed; retrying.");
                delay = ErrorDelay;
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
