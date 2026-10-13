namespace IplStore.Application.Common.Interfaces;

public readonly record struct StockLine(Guid ProductId, int Quantity);

/// <summary>
/// Atomic stock movements. Both methods run on the caller's DbContext, so they join the caller's transaction:
/// a failed checkout rolls the reservation back automatically.
/// </summary>
public interface IInventoryService
{
    /// <exception cref="Domain.Exceptions.InsufficientStockException">Not enough stock; nothing is reserved for that line.</exception>
    /// <exception cref="Domain.Exceptions.NotFoundException">Unknown product.</exception>
    Task ReserveAsync(IReadOnlyCollection<StockLine> lines, CancellationToken ct = default);

    /// <summary>Gives stock back (expired/failed order, or admin restock).</summary>
    Task ReleaseAsync(IReadOnlyCollection<StockLine> lines, CancellationToken ct = default);
}
