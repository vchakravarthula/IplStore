using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Orders;

internal static class OrderLifecycle
{
    /// <summary>
    /// Applies a state transition (Failed / Expired) and gives the reserved stock back in ONE transaction,
    /// so an order can never be cancelled without its stock being released (or vice versa).
    /// The order must be loaded with its Items.
    /// </summary>
    public static async Task TransitionAndReleaseStockAsync(
        IApplicationDbContext db,
        IInventoryService inventory,
        Order order,
        Action<Order> transition,
        CancellationToken ct)
    {
        var stock = order.Items.Select(i => new StockLine(i.ProductId, i.Quantity)).ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            transition(order);
            await inventory.ReleaseAsync(stock, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
