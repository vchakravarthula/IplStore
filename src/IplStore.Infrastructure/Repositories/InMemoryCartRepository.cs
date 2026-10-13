using IplStore.Application.Common.Interfaces;

namespace IplStore.Infrastructure.Repositories;

/// <summary>
/// Single-process cart store for tests and Redis-less local runs (Cart:Store=InMemory).
/// NOT for multi-node deployments: carts would not survive a node loss. Use Redis there.
/// </summary>
public sealed class InMemoryCartRepository : ICartRepository
{
    private readonly Dictionary<string, Dictionary<Guid, int>> _carts = new();
    private readonly object _gate = new();

    public Task<IReadOnlyList<CartLine>> GetAsync(string userId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<CartLine> lines = _carts.TryGetValue(userId, out var cart)
                ? cart.Select(kv => new CartLine(kv.Key, kv.Value)).ToList()
                : Array.Empty<CartLine>();
            return Task.FromResult(lines);
        }
    }

    public Task<int?> AddAsync(string userId, Guid productId, int quantity, int maxPerItem, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var cart = GetOrCreate(userId);
            cart.TryGetValue(productId, out var current);
            var next = current + quantity;
            if (next > maxPerItem) return Task.FromResult<int?>(null);

            cart[productId] = next;
            return Task.FromResult<int?>(next);
        }
    }

    public Task SetQuantityAsync(string userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        lock (_gate) GetOrCreate(userId)[productId] = quantity;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string userId, Guid productId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_carts.TryGetValue(userId, out var cart)) cart.Remove(productId);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(string userId, CancellationToken ct = default)
    {
        lock (_gate) _carts.Remove(userId);
        return Task.CompletedTask;
    }

    private Dictionary<Guid, int> GetOrCreate(string userId)
    {
        if (!_carts.TryGetValue(userId, out var cart))
            _carts[userId] = cart = new Dictionary<Guid, int>();
        return cart;
    }
}
