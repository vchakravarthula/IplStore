using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Common
{
    public class Cart : Entity, IAggregateRoot
    {
        public string UserId { get; private set; } = default!;
        private readonly List<CartItem> _items = new();
        public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

        private Cart() { }

        public Cart(string userId)
        {
            UserId = userId;
        }

        /// <summary>
        /// Rehydrates a Cart aggregate from persistence snapshots without mutating state incorrectly.
        /// </summary>
        public static Cart Rehydrate(string userId, IEnumerable<CartItem> snapshots)
        {
            var cart = new Cart(userId);
            foreach (var snapshot in snapshots)
            {
                cart._items.Add(new CartItem(cart.Id, snapshot.ProductId, snapshot.Quantity, snapshot.UnitPrice));
            }
            return cart;
        }

        public void AddOrUpdateItem(Guid productId, int quantity, decimal unitPrice)
        {
            var existing = _items.FirstOrDefault(i => i.ProductId == productId);
            if (existing is not null)
            {
                existing.UpdateQuantity(existing.Quantity + quantity);
            }
            else
            {
                _items.Add(new CartItem(Id, productId, quantity, unitPrice));
            }
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void RemoveItem(Guid productId)
        {
            _items.RemoveAll(i => i.ProductId == productId);
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void Clear()
        {
            _items.Clear();
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
