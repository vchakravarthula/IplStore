using IplStore.Application.Common.Exceptions;
using IplStore.Application.Common.Interfaces;
using StackExchange.Redis;

namespace IplStore.Infrastructure.Repositories;

/// <summary>
/// One Redis hash per user: key "cart:{userId}", field = productId, value = quantity.
/// Every mutation is a single atomic Redis command (or Lua script), so concurrent adds from two tabs or two
/// API nodes cannot lose updates, and the cart survives any API node dying.
/// </summary>
public sealed class RedisCartRepository : ICartRepository
{
    private static readonly TimeSpan CartTtl = TimeSpan.FromDays(7);

    // Atomic "add with cap": read, check the cap, write and refresh the TTL in one server-side step.
    private static readonly LuaScript AddScript = LuaScript.Prepare(@"
        local current = tonumber(redis.call('HGET', @key, @field) or '0')
        local nxt = current + tonumber(@delta)
        if nxt > tonumber(@max) then return -1 end
        redis.call('HSET', @key, @field, nxt)
        redis.call('EXPIRE', @key, @ttl)
        return nxt");

    private readonly IConnectionMultiplexer _redis;

    public RedisCartRepository(IConnectionMultiplexer redis) => _redis = redis;

    public Task<IReadOnlyList<CartLine>> GetAsync(string userId, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            var entries = await _redis.GetDatabase().HashGetAllAsync(Key(userId));

            var lines = new List<CartLine>(entries.Length);
            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.Name.ToString(), out var productId) && (int)entry.Value > 0)
                    lines.Add(new CartLine(productId, (int)entry.Value));
            }

            return (IReadOnlyList<CartLine>)lines;
        });

    public Task<int?> AddAsync(string userId, Guid productId, int quantity, int maxPerItem, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            var result = await _redis.GetDatabase().ScriptEvaluateAsync(AddScript, new
            {
                key = (RedisKey)Key(userId),
                field = productId.ToString(),
                delta = quantity,
                max = maxPerItem,
                ttl = (int)CartTtl.TotalSeconds
            });

            var value = (int)result;
            return value < 0 ? (int?)null : value;
        });

    public Task SetQuantityAsync(string userId, Guid productId, int quantity, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            var db = _redis.GetDatabase();
            await db.HashSetAsync(Key(userId), productId.ToString(), quantity);
            await db.KeyExpireAsync(Key(userId), CartTtl);
            return true;
        });

    public Task RemoveAsync(string userId, Guid productId, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            await _redis.GetDatabase().HashDeleteAsync(Key(userId), productId.ToString());
            return true;
        });

    public Task ClearAsync(string userId, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            await _redis.GetDatabase().KeyDeleteAsync(Key(userId));
            return true;
        });

    private static string Key(string userId) => $"cart:{userId}";

    // Connection/timeout failures become a 503 with Retry-After instead of a generic 500.
    private static async Task<T> GuardAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new DependencyUnavailableException("Cart store", ex);
        }
    }
}
