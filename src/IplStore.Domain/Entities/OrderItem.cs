using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
    public class OrderItem : Entity
    {
        public Guid OrderId { get; private set; }
        public Guid ProductId { get; private set; }
        public string ProductName { get; private set; } = default!;
        public int Quantity { get; private set; }

        // Immutable Price Snapshot taken at checkout time
        public decimal UnitPrice { get; private set; }
        public decimal TotalPrice => Quantity * UnitPrice;

        private OrderItem() { }

        internal OrderItem(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
        {
            OrderId = orderId;
            ProductId = productId;
            ProductName = productName;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
}
