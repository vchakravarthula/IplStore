using IplStore.Application.Orders.Commands;
using IplStore.Application.Orders.Queries;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Repositories;
using IplStore.Infrastructure.Services;
using IplStore.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Application;

public sealed class GetOrdersQueryHandlerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InMemoryCartRepository _cart = new();
    private readonly FakePaymentGateway _gateway = new();

    public void Dispose() => _db.Dispose();

    private async Task<Guid> PlaceOrderAsync(string user, Guid productId)
    {
        await _cart.AddAsync(user, productId, 1, 10);
        var ctx = _db.CreateContext();
        var checkout = new CheckoutCommandHandler(ctx, _cart, new SqlInventoryService(ctx), _gateway, NullLogger<CheckoutCommandHandler>.Instance);
        return (await checkout.HandleAsync(new CheckoutCommand(user, Guid.NewGuid().ToString("N")))).OrderId;
    }

    [Fact]
    public async Task History_contains_only_the_callers_orders_newest_first()
    {
        var product = await _db.SeedProductAsync(stock: 50);
        var aliceFirst = await PlaceOrderAsync("alice", product.Id);
        await Task.Delay(5);
        var aliceSecond = await PlaceOrderAsync("alice", product.Id);
        await PlaceOrderAsync("bob", product.Id);

        var history = await new GetOrdersQueryHandler(_db.CreateContext()).ListAsync("alice");

        Assert.Equal(2, history.TotalCount);
        Assert.Equal(new[] { aliceSecond, aliceFirst }, history.Items.Select(o => o.Id).ToArray());
    }

    [Fact]
    public async Task Order_detail_is_scoped_to_the_owner()
    {
        var product = await _db.SeedProductAsync(stock: 50);
        var orderId = await PlaceOrderAsync("alice", product.Id);
        var handler = new GetOrdersQueryHandler(_db.CreateContext());

        var detail = await handler.GetByIdAsync("alice", orderId);

        Assert.Single(detail.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.GetByIdAsync("bob", orderId));
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var result = await new GetOrdersQueryHandler(_db.CreateContext()).ListAsync("alice", pageSize: 10_000);
        Assert.Equal(GetOrdersQueryHandler.MaxPageSize, result.PageSize);
    }
}
