using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Orders.Commands;

/// <summary>
/// Reservation expiry: orders still PendingPayment after <c>maxAge</c> are cancelled and their stock is
/// released, so abandoned checkouts can never hold inventory hostage.
/// </summary>
public sealed class ExpirePendingOrdersCommandHandler
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILogger<ExpirePendingOrdersCommandHandler> _logger;

    public ExpirePendingOrdersCommandHandler(
        IApplicationDbContext db,
        IInventoryService inventory,
        ILogger<ExpirePendingOrdersCommandHandler> logger)
    {
        _db = db;
        _inventory = inventory;
        _logger = logger;
    }

    public async Task<int> HandleAsync(TimeSpan maxAge, int batchSize = 50, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - maxAge;

        var staleIds = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAtUtc < cutoff)
            .OrderBy(o => o.CreatedAtUtc)
            .Select(o => o.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in staleIds)
        {
            // Fresh load per order: if a payment confirmation raced us, we see (or lose to) the newer state.
            var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null || order.Status != OrderStatus.PendingPayment) continue;

            try
            {
                await OrderLifecycle.TransitionAndReleaseStockAsync(_db, _inventory, order, o => o.Expire(), ct);
                expired++;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation("Order {OrderId} changed while expiring (likely just paid); skipped.", id);
                _db.ChangeTracker.Clear();
            }
        }

        return expired;
    }
}
