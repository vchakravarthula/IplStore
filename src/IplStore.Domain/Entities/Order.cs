using IplStore.Domain.Common;
using IplStore.Domain.Events;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Entities;

public class Order : Entity, IAggregateRoot
{
    public const int MaxIdempotencyKeyLength = 64;

    public string UserId { get; private set; } = default!;
    // Client-supplied per checkout attempt. (UserId, IdempotencyKey) is unique, so a retry returns the same order.
    public string IdempotencyKey { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public string ShortOrderNumber { get; private set; } = default!;
    public string? GatewayOrderId { get; private set; }
    public string? PaymentTransactionId { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public Order(string userId, string idempotencyKey, IEnumerable<OrderLine> lines)
    {
        // Validate everything BEFORE mutating any state.
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("UserId is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
            throw new ValidationException($"Idempotency key is required (max {MaxIdempotencyKeyLength} characters).");

        var orderLines = lines.ToList();
        if (orderLines.Count == 0)
            throw new ValidationException("An order needs at least one item.");

        foreach (var line in orderLines)
        {
            if (line.ProductId == Guid.Empty) throw new ValidationException("Order line has no product.");
            if (string.IsNullOrWhiteSpace(line.ProductName)) throw new ValidationException("Order line has no product name.");
            if (line.Quantity <= 0) throw new ValidationException("Order line quantity must be greater than zero.");
            if (line.UnitPrice < 0) throw new ValidationException("Order line price cannot be negative.");
        }

        UserId = userId;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.PendingPayment;
        ShortOrderNumber = OrderNumberGenerator.From(Id);

        foreach (var line in orderLines)
            _items.Add(new OrderItem(Id, line.ProductId, line.ProductName, line.Quantity, line.UnitPrice));

        TotalAmount = _items.Sum(i => i.TotalPrice);

        AddDomainEvent(new OrderPlaced(Id, UserId, ShortOrderNumber, TotalAmount));
    }

    public void AttachGatewayOrder(string gatewayOrderId)
    {
        if (string.IsNullOrWhiteSpace(gatewayOrderId))
            throw new ValidationException("Gateway order id is required.");
        if (Status != OrderStatus.PendingPayment)
            throw new ConflictException($"Order {ShortOrderNumber} is {Status}; a payment cannot be attached.");

        GatewayOrderId = gatewayOrderId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Idempotent: confirming the same payment twice (client + webhook) is a no-op.</summary>
    public void MarkAsPaid(string paymentId)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new ValidationException("Payment id is required.");

        if (Status == OrderStatus.Paid && PaymentTransactionId == paymentId)
            return;

        if (Status != OrderStatus.PendingPayment)
            throw new ConflictException($"Order {ShortOrderNumber} is {Status} and cannot be marked as paid.");

        Status = OrderStatus.Paid;
        PaymentTransactionId = paymentId;
        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new OrderPaid(Id, UserId, ShortOrderNumber, TotalAmount, paymentId));
    }

    public void MarkAsFailed()
    {
        if (Status == OrderStatus.Failed) return;
        if (Status != OrderStatus.PendingPayment)
            throw new ConflictException($"Order {ShortOrderNumber} is {Status} and cannot be marked as failed.");

        Status = OrderStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Called by the reservation-expiry job when payment never arrived.</summary>
    public void Expire()
    {
        if (Status == OrderStatus.Cancelled) return;
        if (Status != OrderStatus.PendingPayment)
            throw new ConflictException($"Order {ShortOrderNumber} is {Status} and cannot be expired.");

        Status = OrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
