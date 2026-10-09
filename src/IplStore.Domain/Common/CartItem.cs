using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Common
{
    public class CartItem : Entity
    {
        public Guid CartId { get; private set; }
        public Guid ProductId { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }

        private CartItem() { }

        public CartItem(Guid cartId, Guid productId, int quantity, decimal unitPrice)
        {
            CartId = cartId;
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }

        internal void UpdateQuantity(int newQuantity)
        {
            if (newQuantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.", nameof(newQuantity));

            Quantity = newQuantity;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
