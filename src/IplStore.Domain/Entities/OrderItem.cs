using IplStore.Domain.Common;

namespace IplStore.Domain.Entities;

public class OrderItem : Entity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }

    // Immutable price snapshot taken at checkout: later catalog price changes never rewrite history.
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    private OrderItem() { }

    // internal: only the Order aggregate may create its items.
    internal OrderItem(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
