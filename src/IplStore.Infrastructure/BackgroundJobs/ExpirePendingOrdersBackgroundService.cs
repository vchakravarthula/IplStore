using IplStore.Application.Orders.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.BackgroundJobs;

/// <summary>Releases stock held by orders whose payment never arrived (default: 15 minutes).</summary>
public sealed class ExpirePendingOrdersBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpirePendingOrdersBackgroundService> _logger;
    private readonly TimeSpan _maxAge;

    public ExpirePendingOrdersBackgroundService(
        IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ExpirePendingOrdersBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _maxAge = TimeSpan.FromMinutes(configuration.GetValue("Orders:PendingPaymentTimeoutMinutes", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExpirePendingOrdersCommandHandler>();
                var expired = await handler.HandleAsync(_maxAge, ct: stoppingToken);
                if (expired > 0) _logger.LogInformation("Expired {Count} unpaid orders and released their stock.", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order expiry run failed; will retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
