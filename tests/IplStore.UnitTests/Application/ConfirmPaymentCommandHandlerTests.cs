using IplStore.Application.Orders.Commands;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Repositories;
using IplStore.Infrastructure.Services;
using IplStore.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Application;

public sealed class ConfirmPaymentCommandHandlerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InMemoryCartRepository _cart = new();
    private readonly FakePaymentGateway _gateway = new();
    private Guid _productId;

    public void Dispose() => _db.Dispose();

    private async Task<CheckoutResponse> PlaceOrderAsync(string user = "u1")
    {
        var product = await _db.SeedProductAsync(stock: 10);
        _productId = product.Id;
        await _cart.AddAsync(user, product.Id, 1, 10);
        var ctx = _db.CreateContext();
        var handler = new CheckoutCommandHandler(ctx, _cart, new SqlInventoryService(ctx), _gateway, NullLogger<CheckoutCommandHandler>.Instance);
        return await handler.HandleAsync(new CheckoutCommand(user, Guid.NewGuid().ToString("N")));
    }

    private ConfirmPaymentCommandHandler NewHandler() =>
        new(_db.CreateContext(), _gateway, _cart, NullLogger<ConfirmPaymentCommandHandler>.Instance);

    [Fact]
    public async Task Valid_confirmation_marks_the_order_paid_and_emits_OrderPaid()
    {
        var order = await PlaceOrderAsync();

        var status = await NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "sig"));

        Assert.Equal("Paid", status);
        await using var check = _db.CreateContext();
        Assert.True(await check.OutboxMessages.AnyAsync(m => m.EventType == "OrderPaid"));
    }

    [Fact]
    public async Task Confirming_the_same_payment_twice_is_idempotent()
    {
        var order = await PlaceOrderAsync();
        var command = new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "sig");

        await NewHandler().HandleAsync(command);
        var status = await NewHandler().HandleAsync(command);

        Assert.Equal("Paid", status);
        await using var check = _db.CreateContext();
        Assert.Equal(1, await check.OutboxMessages.CountAsync(m => m.EventType == "OrderPaid"));
    }

    [Fact]
    public async Task Invalid_signature_is_rejected_and_the_order_stays_pending()
    {
        var order = await PlaceOrderAsync();
        _gateway.SignaturesAreValid = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "forged")));

        await using var check = _db.CreateContext();
        Assert.Equal(OrderStatus.PendingPayment, (await check.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task Another_users_order_looks_like_it_does_not_exist()
    {
        var order = await PlaceOrderAsync("alice");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewHandler().HandleAsync(new ConfirmPaymentCommand("mallory", order.OrderId, order.GatewayOrderId!, "pay_1", "sig")));
    }

    [Fact]
    public async Task A_payment_for_a_different_gateway_order_is_rejected()
    {
        var order = await PlaceOrderAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", order.OrderId, "order_someone_else", "pay_1", "sig")));
    }

    [Fact]
    public async Task The_same_payment_id_cannot_settle_two_orders()
    {
        var first = await PlaceOrderAsync("u1");
        var second = await PlaceOrderAsync("u2");

        await NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", first.OrderId, first.GatewayOrderId!, "pay_1", "sig"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler().HandleAsync(new ConfirmPaymentCommand("u2", second.OrderId, second.GatewayOrderId!, "pay_1", "sig")));
    }

    [Fact]
    public async Task Paying_removes_only_the_paid_items_from_the_cart()
    {
        var order = await PlaceOrderAsync();
        var other = await _db.SeedProductAsync(name: "Other", code: "ZZZ");
        await _cart.AddAsync("u1", other.Id, 1, 10);   // added after checkout

        await NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "sig"));

        Assert.Equal(other.Id, Assert.Single(await _cart.GetAsync("u1")).ProductId);
    }

    [Fact]
    public async Task A_rejected_confirmation_keeps_the_cart_so_the_customer_can_try_again()
    {
        var order = await PlaceOrderAsync();
        _gateway.SignaturesAreValid = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewHandler().HandleAsync(new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "forged")));

        Assert.Single(await _cart.GetAsync("u1"));
    }

    [Fact]
    public async Task Replaying_a_confirmation_does_not_remove_items_the_customer_added_after_paying()
    {
        var order = await PlaceOrderAsync();
        var command = new ConfirmPaymentCommand("u1", order.OrderId, order.GatewayOrderId!, "pay_1", "sig");
        await NewHandler().HandleAsync(command);

        await _cart.AddAsync("u1", _productId, 1, 10);   // shopping again for the same product
        await NewHandler().HandleAsync(command);          // double click / duplicate callback

        Assert.Equal(1, Assert.Single(await _cart.GetAsync("u1")).Quantity);
    }
}
