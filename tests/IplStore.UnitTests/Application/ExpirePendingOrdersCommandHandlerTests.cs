using IplStore.Application.Orders.Commands;
using IplStore.Domain.Entities;
using IplStore.Infrastructure.Repositories;
using IplStore.Infrastructure.Services;
using IplStore.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Application;

public sealed class ExpirePendingOrdersCommandHandlerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InMemoryCartRepository _cart = new();
    private readonly FakePaymentGateway _gateway = new();

    public void Dispose() => _db.Dispose();

    private async Task<(Guid ProductId, Guid OrderId)> PlaceOrderAsync(int qty = 3)
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await _cart.AddAsync("u1", product.Id, qty, 10);
        var ctx = _db.CreateContext();
        var checkout = new CheckoutCommandHandler(ctx, _cart, new SqlInventoryService(ctx), _gateway, NullLogger<CheckoutCommandHandler>.Instance);
        var response = await checkout.HandleAsync(new CheckoutCommand("u1", Guid.NewGuid().ToString("N")));
        return (product.Id, response.OrderId);
    }

    private ExpirePendingOrdersCommandHandler NewHandler()
    {
        var ctx = _db.CreateContext();
        return new ExpirePendingOrdersCommandHandler(ctx, new SqlInventoryService(ctx), NullLogger<ExpirePendingOrdersCommandHandler>.Instance);
    }

    [Fact]
    public async Task Stale_unpaid_orders_are_cancelled_and_their_stock_is_released()
    {
        var (productId, orderId) = await PlaceOrderAsync(qty: 3);
        Assert.Equal(7, await _db.StockOfAsync(productId));

        var expired = await NewHandler().HandleAsync(maxAge: TimeSpan.Zero);

        Assert.Equal(1, expired);
        Assert.Equal(10, await _db.StockOfAsync(productId));
        await using var check = _db.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await check.Orders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Orders_younger_than_the_timeout_are_left_alone()
    {
        var (productId, _) = await PlaceOrderAsync(qty: 3);

        var expired = await NewHandler().HandleAsync(maxAge: TimeSpan.FromMinutes(15));

        Assert.Equal(0, expired);
        Assert.Equal(7, await _db.StockOfAsync(productId));
    }

    [Fact]
    public async Task Paid_orders_are_never_expired()
    {
        var (productId, orderId) = await PlaceOrderAsync(qty: 3);
        await using (var ctx = _db.CreateContext())
        {
            var order = await ctx.Orders.SingleAsync(o => o.Id == orderId);
            order.MarkAsPaid("pay_1");
            await ctx.SaveChangesAsync();
        }

        var expired = await NewHandler().HandleAsync(maxAge: TimeSpan.Zero);

        Assert.Equal(0, expired);
        Assert.Equal(7, await _db.StockOfAsync(productId));
    }

    [Fact]
    public async Task Running_twice_releases_stock_only_once()
    {
        var (productId, _) = await PlaceOrderAsync(qty: 3);

        await NewHandler().HandleAsync(maxAge: TimeSpan.Zero);
        await NewHandler().HandleAsync(maxAge: TimeSpan.Zero);

        Assert.Equal(10, await _db.StockOfAsync(productId));
    }
}
