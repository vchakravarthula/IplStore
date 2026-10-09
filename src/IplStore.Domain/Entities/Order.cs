using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
    public class Order : Entity, IAggregateRoot
    {
        public string UserId { get; private set; } = default!;
        public decimal TotalAmount { get; private set; }
        public OrderStatus Status { get; private set; }
        public string? PaymentTransactionId { get; private set; }
        public string ShortOrderNumber { get; private set; } = default!;

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        private Order() { }

        public Order(string userId, IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items)
        {
            UserId = userId;
            Status = OrderStatus.PendingPayment;
            ShortOrderNumber = OrderNumberGenerator.Generate();

            foreach (var item in items)
            {
                _items.Add(new OrderItem(Id, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice));
            }

            TotalAmount = _items.Sum(i => i.TotalPrice);
        }

        public void MarkAsPaid(string transactionId)
        {
            Status = OrderStatus.Paid;
            PaymentTransactionId = transactionId;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void MarkAsFailed()
        {
            Status = OrderStatus.Failed;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
