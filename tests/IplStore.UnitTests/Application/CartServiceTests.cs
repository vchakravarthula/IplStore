using IplStore.Application.Cart;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Repositories;
using IplStore.UnitTests.Support;

namespace IplStore.UnitTests.Application;

public sealed class CartServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly InMemoryCartRepository _store = new();

    public void Dispose() => _db.Dispose();

    private CartService NewService() => new(_db.CreateContext(), _store);

    [Fact]
    public async Task Add_returns_the_cart_priced_from_the_catalog()
    {
        var product = await _db.SeedProductAsync(price: 499m);

        var cart = await NewService().AddAsync("u1", product.Id, 2);

        var item = Assert.Single(cart.Items);
        Assert.Equal(499m, item.UnitPrice);
        Assert.Equal(998m, item.LineTotal);
        Assert.Equal(998m, cart.Total);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_accumulates_the_quantity()
    {
        var product = await _db.SeedProductAsync();
        await NewService().AddAsync("u1", product.Id, 2);

        var cart = await NewService().AddAsync("u1", product.Id, 3);

        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task The_per_item_cap_is_enforced_across_adds()
    {
        var product = await _db.SeedProductAsync(stock: 100);
        await NewService().AddAsync("u1", product.Id, CartRules.MaxQuantityPerItem);

        await Assert.ThrowsAsync<ValidationException>(() => NewService().AddAsync("u1", product.Id, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CartRules.MaxQuantityPerItem + 1)]
    public async Task Out_of_range_quantities_are_rejected_before_anything_is_stored(int quantity)
    {
        var product = await _db.SeedProductAsync(stock: 100);

        await Assert.ThrowsAsync<ValidationException>(() => NewService().AddAsync("u1", product.Id, quantity));
        Assert.Empty(await _store.GetAsync("u1"));
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => NewService().AddAsync("u1", Guid.NewGuid(), 1));

    [Fact]
    public async Task Cannot_add_more_than_is_in_stock()
    {
        var product = await _db.SeedProductAsync(stock: 1);
        await Assert.ThrowsAsync<InsufficientStockException>(() => NewService().AddAsync("u1", product.Id, 2));
    }

    [Fact]
    public async Task Remove_really_removes_the_item_even_when_other_items_remain()
    {
        var a = await _db.SeedProductAsync(name: "A", code: "AAA");
        var b = await _db.SeedProductAsync(name: "B", code: "BBB");
        await NewService().AddAsync("u1", a.Id, 1);
        await NewService().AddAsync("u1", b.Id, 1);

        var cart = await NewService().RemoveAsync("u1", a.Id);

        Assert.Equal(b.Id, Assert.Single(cart.Items).ProductId);
    }

    [Fact]
    public async Task Carts_are_isolated_per_user()
    {
        var product = await _db.SeedProductAsync();
        await NewService().AddAsync("alice", product.Id, 1);

        Assert.Empty((await NewService().GetAsync("bob")).Items);
    }

    [Fact]
    public async Task Set_quantity_replaces_rather_than_adds()
    {
        var product = await _db.SeedProductAsync();
        await NewService().AddAsync("u1", product.Id, 5);

        var cart = await NewService().SetQuantityAsync("u1", product.Id, 2);

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }
}
