using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IplStore.Infrastructure.Repositories
{
    public class RedisCartRepository : ICartRepository
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private static readonly TimeSpan CartExpiry = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            // Tells System.Text.Json to use parameterless non-public constructors
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
        };

        public RedisCartRepository(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
        }
        private static string GetKey(string userId) => $"cart:{userId}";

        public async Task<Cart?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            var key = GetKey(userId);
            var entries = await _db.HashGetAllAsync(key);

            if (entries.Length == 0) return null;

            var snapshots = new List<CartItem>();

            foreach (var entry in entries)
            {
                if (!entry.Value.IsNullOrEmpty)
                {
                    var snapshot = JsonSerializer.Deserialize<CartItem>((string)entry.Value!, JsonOptions);
                    if (snapshot is not null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return Cart.Rehydrate(userId, snapshots);
        }

        public async Task SaveCartAsync(Cart cart, CancellationToken ct = default)
        {
            var key = GetKey(cart.UserId);

            if (!cart.Items.Any())
            {
                await DeleteCartAsync(cart.UserId, ct);
                return;
            }

            // Convert cart items to atomic Redis Hash entries
            var hashEntries = cart.Items.Select(item => new HashEntry(
                item.ProductId.ToString(),
                JsonSerializer.Serialize(new CartItem(item.CartId, item.ProductId, item.Quantity, item.UnitPrice), JsonOptions)
            )).ToArray();

            var batch = _db.CreateBatch();
            _ = batch.HashSetAsync(key, hashEntries);
            _ = batch.KeyExpireAsync(key, CartExpiry);
            batch.Execute();
        }

        public async Task RemoveItemAsync(string userId, Guid productId, CancellationToken ct = default)
        {
            var key = GetKey(userId);
            await _db.HashDeleteAsync(key, productId.ToString());
        }

        public async Task DeleteCartAsync(string userId, CancellationToken ct = default)
        {
            await _db.KeyDeleteAsync(GetKey(userId));
        }   
    }
}
