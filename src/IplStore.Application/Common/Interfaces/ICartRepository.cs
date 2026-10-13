namespace IplStore.Application.Common.Interfaces;

public sealed record CartLine(Guid ProductId, int Quantity);

/// <summary>
/// The cart is ephemeral per-user state, not an aggregate: every operation is a single atomic store command,
/// so concurrent requests (two tabs, two nodes) can never lose an update.
/// </summary>
public interface ICartRepository
{
    Task<IReadOnlyList<CartLine>> GetAsync(string userId, CancellationToken ct = default);

    /// <returns>The new quantity, or null when adding would exceed <paramref name="maxPerItem"/> (nothing is written).</returns>
    Task<int?> AddAsync(string userId, Guid productId, int quantity, int maxPerItem, CancellationToken ct = default);

    Task SetQuantityAsync(string userId, Guid productId, int quantity, CancellationToken ct = default);

    Task RemoveAsync(string userId, Guid productId, CancellationToken ct = default);

    Task ClearAsync(string userId, CancellationToken ct = default);
}
