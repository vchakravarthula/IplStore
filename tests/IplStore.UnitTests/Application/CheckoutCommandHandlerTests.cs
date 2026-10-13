using IplStore.Application.Common.Interfaces;
using IplStore.Application.Orders.Commands;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Repositories;
using IplStore.Infrastructure.Services;
using IplStore.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Application;

public sealed class CheckoutCommandHandlerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InMemoryCartRepository _cart = new();
    private readonly FakePaymentGateway _gateway = new();

    public void Dispose() => _db.Dispose();

    // A new context per call = a new HTTP request (scoped DbContext), sharing the same database.
    private CheckoutCommandHandler NewHandler()
    {
        var ctx = _db.CreateContext();
        return new CheckoutCommandHandler(ctx, _cart, new SqlInventoryService(ctx), _gateway, NullLogger<CheckoutCommandHandler>.Instance);
    }

    [Fact]
    public async Task Checkout_creates_a_pending_order_reserves_stock_attaches_gateway_order_and_keeps_the_cart_until_payment()
    {
        var product = await _db.SeedProductAsync(stock: 10, price: 250m);
        await _cart.AddAsync("u1", product.Id, 3, 10);

        var response = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-1"));

        Assert.Equal(750m, response.TotalAmount);
        Assert.Equal("INR", response.Currency);
        Assert.Equal("PendingPayment", response.Status);
        Assert.NotNull(response.GatewayOrderId);
        Assert.Equal(7, await _db.StockOfAsync(product.Id));
        Assert.Single(await _cart.GetAsync("u1"));   // the cart stays until the payment succeeds

        await using var check = _db.CreateContext();
        var order = await check.Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(250m, order.Items.Single().UnitPrice);   // price snapshot comes from the catalog
        Assert.Equal(response.GatewayOrderId, order.GatewayOrderId);
    }

    [Fact]
    public async Task Checkout_writes_an_OrderPlaced_outbox_row_in_the_same_transaction()
    {
        var product = await _db.SeedProductAsync();
        await _cart.AddAsync("u1", product.Id, 1, 10);

        await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-1"));

        await using var check = _db.CreateContext();
        var message = await check.OutboxMessages.SingleAsync();
        Assert.Equal("OrderPlaced", message.EventType);
        Assert.Contains("\"TotalAmount\":100", message.Payload);
    }

    [Fact]
    public async Task Not_enough_stock_throws_and_leaves_stock_cart_and_orders_untouched()
    {
        var product = await _db.SeedProductAsync(stock: 2);
        await _cart.AddAsync("u1", product.Id, 5, 10);

        await Assert.ThrowsAsync<InsufficientStockException>(() => NewHandler().HandleAsync(new CheckoutCommand("u1", "key-1")));

        Assert.Equal(2, await _db.StockOfAsync(product.Id));
        Assert.Single(await _cart.GetAsync("u1"));
        await using var check = _db.CreateContext();
        Assert.False(await check.Orders.AnyAsync());
    }

    [Fact]
    public async Task Last_unit_goes_to_the_first_buyer_and_the_second_gets_a_clean_409_style_error()
    {
        var product = await _db.SeedProductAsync(stock: 1);
        await _cart.AddAsync("alice", product.Id, 1, 10);
        await _cart.AddAsync("bob", product.Id, 1, 10);

        await NewHandler().HandleAsync(new CheckoutCommand("alice", "a-1"));
        await Assert.ThrowsAsync<InsufficientStockException>(() => NewHandler().HandleAsync(new CheckoutCommand("bob", "b-1")));

        Assert.Equal(0, await _db.StockOfAsync(product.Id));   // never negative: no oversell
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_returns_the_same_order_and_deducts_stock_once()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, 2, 10);

        var first = await NewHandler().HandleAsync(new CheckoutCommand("u1", "same-key"));
        var second = await NewHandler().HandleAsync(new CheckoutCommand("u1", "same-key"));

        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(first.GatewayOrderId, second.GatewayOrderId);
        Assert.Equal(8, await _db.StockOfAsync(product.Id));
        Assert.Equal(1, _gateway.CreateCalls);
    }

    [Fact]
    public async Task A_second_checkout_with_an_unchanged_cart_reuses_the_open_order_even_with_a_new_key()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, 2, 10);

        var first = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-A"));
        var second = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-B"));   // e.g. the user left the page and came back

        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(first.GatewayOrderId, second.GatewayOrderId);
        Assert.Equal(8, await _db.StockOfAsync(product.Id));   // reserved once
        Assert.Equal(1, _gateway.CreateCalls);
    }

    [Fact]
    public async Task Changing_the_cart_releases_the_old_open_order_and_reserves_again()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, 2, 10);
        var first = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-A"));

        await _cart.AddAsync("u1", product.Id, 1, 10);   // the cart is now 3
        var second = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-B"));

        Assert.NotEqual(first.OrderId, second.OrderId);
        Assert.Equal(7, await _db.StockOfAsync(product.Id));   // 10 - 3: the first reservation was given back
        await using var check = _db.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await check.Orders.SingleAsync(o => o.Id == first.OrderId)).Status);
        Assert.Equal(OrderStatus.PendingPayment, (await check.Orders.SingleAsync(o => o.Id == second.OrderId)).Status);
    }

    [Fact]
    public async Task A_price_change_after_checkout_creates_a_fresh_order_at_the_new_price()
    {
        var product = await _db.SeedProductAsync(stock: 10, price: 100m);
        await _cart.AddAsync("u1", product.Id, 1, 10);
        var first = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-A"));

        await using (var ctx = _db.CreateContext())
            await ctx.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 150m));
        var second = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-B"));

        Assert.NotEqual(first.OrderId, second.OrderId);
        Assert.Equal(150m, second.TotalAmount);
        Assert.Equal(9, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task A_failed_checkout_that_cannot_reserve_leaves_the_old_open_order_untouched()
    {
        var product = await _db.SeedProductAsync(stock: 4);
        await _cart.AddAsync("u1", product.Id, 2, 10);
        var first = await NewHandler().HandleAsync(new CheckoutCommand("u1", "key-A"));   // stock now 2

        await _cart.AddAsync("u1", product.Id, 5, 10);                                    // cart 7: more than exists
        await Assert.ThrowsAsync<InsufficientStockException>(() => NewHandler().HandleAsync(new CheckoutCommand("u1", "key-B")));

        await using var check = _db.CreateContext();
        Assert.Equal(OrderStatus.PendingPayment, (await check.Orders.SingleAsync(o => o.Id == first.OrderId)).Status);   // rolled back together
        Assert.Equal(2, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Gateway_failure_compensates_order_failed_and_stock_released()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, 4, 10);
        _gateway.FailOnCreate = true;

        await Assert.ThrowsAsync<IplStore.Application.Common.Exceptions.PaymentGatewayException>(
            () => NewHandler().HandleAsync(new CheckoutCommand("u1", "k")));

        Assert.Equal(10, await _db.StockOfAsync(product.Id));   // reservation given back
        await using var check = _db.CreateContext();
        Assert.Equal(OrderStatus.Failed, (await check.Orders.SingleAsync()).Status);
        Assert.Single(await _cart.GetAsync("u1"));               // cart kept so the user can retry
    }

    [Fact]
    public async Task Retry_after_a_gateway_failure_with_the_same_key_does_not_resurrect_the_failed_order()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, 1, 10);
        _gateway.FailOnCreate = true;
        await Assert.ThrowsAsync<IplStore.Application.Common.Exceptions.PaymentGatewayException>(
            () => NewHandler().HandleAsync(new CheckoutCommand("u1", "k")));

        _gateway.FailOnCreate = false;
        var retry = await NewHandler().HandleAsync(new CheckoutCommand("u1", "k"));

        Assert.Equal("Failed", retry.Status);   // the key is spent; the client must start a new attempt with a new key
        Assert.Equal(10, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Empty_cart_is_a_validation_error()
        => await Assert.ThrowsAsync<ValidationException>(() => NewHandler().HandleAsync(new CheckoutCommand("u1", "k")));

    [Theory]
    [InlineData("", "k")]
    [InlineData("u1", "")]
    public async Task Missing_user_or_idempotency_key_is_rejected(string userId, string key)
        => await Assert.ThrowsAsync<ValidationException>(() => NewHandler().HandleAsync(new CheckoutCommand(userId, key)));

    [Fact]
    public async Task Order_history_isolation_two_users_can_use_the_same_key()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("alice", product.Id, 1, 10);
        await _cart.AddAsync("bob", product.Id, 1, 10);

        var a = await NewHandler().HandleAsync(new CheckoutCommand("alice", "k"));
        var b = await NewHandler().HandleAsync(new CheckoutCommand("bob", "k"));

        Assert.NotEqual(a.OrderId, b.OrderId);
    }
}
