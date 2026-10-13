using IplStore.Domain.Entities;
using IplStore.Domain.Events;
using IplStore.Domain.Exceptions;

namespace IplStore.UnitTests.Domain;

public class OrderTests
{
    private static readonly Guid P1 = Guid.NewGuid();
    private static readonly Guid P2 = Guid.NewGuid();

    private static Order NewOrder(string key = "key-1") => new(
        "user-1", key,
        new[]
        {
            new OrderLine(P1, "CSK Jersey", 2, 1999m),
            new OrderLine(P2, "CSK Cap", 1, 599m)
        });

    [Fact]
    public void New_order_snapshots_prices_computes_total_and_raises_OrderPlaced()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(2 * 1999m + 599m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.Matches("^IPL-[0-9A-Z]{10}$", order.ShortOrderNumber);

        var placed = Assert.Single(order.DomainEvents.OfType<OrderPlaced>());
        Assert.Equal(order.Id, placed.OrderId);
        Assert.Equal(order.TotalAmount, placed.TotalAmount);
    }

    [Fact]
    public void Constructor_rejects_blank_user()
        => Assert.Throws<ValidationException>(() => new Order(" ", "k", new[] { new OrderLine(P1, "x", 1, 1m) }));

    [Fact]
    public void Constructor_rejects_missing_or_too_long_idempotency_key()
    {
        var lines = new[] { new OrderLine(P1, "x", 1, 1m) };
        Assert.Throws<ValidationException>(() => new Order("u", "", lines));
        Assert.Throws<ValidationException>(() => new Order("u", new string('k', Order.MaxIdempotencyKeyLength + 1), lines));
    }

    [Fact]
    public void Constructor_rejects_empty_order()
        => Assert.Throws<ValidationException>(() => new Order("u", "k", Array.Empty<OrderLine>()));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_rejects_non_positive_quantity(int quantity)
        => Assert.Throws<ValidationException>(() => new Order("u", "k", new[] { new OrderLine(P1, "x", quantity, 10m) }));

    [Fact]
    public void Constructor_rejects_negative_price()
        => Assert.Throws<ValidationException>(() => new Order("u", "k", new[] { new OrderLine(P1, "x", 1, -1m) }));

    [Fact]
    public void MarkAsPaid_moves_to_Paid_and_raises_OrderPaid_once()
    {
        var order = NewOrder();
        order.ClearDomainEvents();

        order.MarkAsPaid("pay_1");
        order.MarkAsPaid("pay_1"); // replay (client confirm + webhook) must be a no-op

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("pay_1", order.PaymentTransactionId);
        Assert.Single(order.DomainEvents.OfType<OrderPaid>());
    }

    [Fact]
    public void MarkAsPaid_with_a_different_payment_after_paid_is_a_conflict()
    {
        var order = NewOrder();
        order.MarkAsPaid("pay_1");
        Assert.Throws<ConflictException>(() => order.MarkAsPaid("pay_2"));
    }

    [Fact]
    public void Expired_order_cannot_be_paid()
    {
        var order = NewOrder();
        order.Expire();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Throws<ConflictException>(() => order.MarkAsPaid("pay_1"));
    }

    [Fact]
    public void Paid_order_cannot_be_failed_or_expired()
    {
        var order = NewOrder();
        order.MarkAsPaid("pay_1");
        Assert.Throws<ConflictException>(() => order.MarkAsFailed());
        Assert.Throws<ConflictException>(() => order.Expire());
    }

    [Fact]
    public void MarkAsFailed_and_Expire_are_idempotent()
    {
        var failed = NewOrder("a");
        failed.MarkAsFailed();
        failed.MarkAsFailed();
        Assert.Equal(OrderStatus.Failed, failed.Status);

        var expired = NewOrder("b");
        expired.Expire();
        expired.Expire();
        Assert.Equal(OrderStatus.Cancelled, expired.Status);
    }

    [Fact]
    public void AttachGatewayOrder_requires_pending_order_and_value()
    {
        var order = NewOrder();
        Assert.Throws<ValidationException>(() => order.AttachGatewayOrder(" "));

        order.AttachGatewayOrder("order_abc");
        Assert.Equal("order_abc", order.GatewayOrderId);

        order.Expire();
        Assert.Throws<ConflictException>(() => order.AttachGatewayOrder("order_def"));
    }
}
