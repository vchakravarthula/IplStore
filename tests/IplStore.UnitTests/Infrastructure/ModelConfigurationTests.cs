using IplStore.Domain.Entities;
using IplStore.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IplStore.UnitTests.Infrastructure;

/// <summary>Guards the database-level invariants the application logic relies on.</summary>
public sealed class ModelConfigurationTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Order NewOrder(string user, string key, Guid productId) =>
        new(user, key, new[] { new OrderLine(productId, "P", 1, 10m) });

    [Fact]
    public async Task Two_orders_with_the_same_user_and_idempotency_key_cannot_both_be_stored()
    {
        var product = await _db.SeedProductAsync();
        await using (var ctx = _db.CreateContext())
        {
            ctx.Orders.Add(NewOrder("u1", "k", product.Id));
            await ctx.SaveChangesAsync();
        }

        await using var second = _db.CreateContext();
        second.Orders.Add(NewOrder("u1", "k", product.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_key_is_fine_for_a_different_user()
    {
        var product = await _db.SeedProductAsync();
        await using var ctx = _db.CreateContext();
        ctx.Orders.Add(NewOrder("u1", "k", product.Id));
        ctx.Orders.Add(NewOrder("u2", "k", product.Id));

        await ctx.SaveChangesAsync();
        Assert.Equal(2, await ctx.Orders.CountAsync());
    }

    [Fact]
    public async Task A_gateway_order_id_can_belong_to_only_one_order()
    {
        var product = await _db.SeedProductAsync();
        await using (var ctx = _db.CreateContext())
        {
            var a = NewOrder("u1", "k1", product.Id);
            a.AttachGatewayOrder("order_X");
            ctx.Orders.Add(a);
            await ctx.SaveChangesAsync();
        }

        await using var second = _db.CreateContext();
        var b = NewOrder("u2", "k2", product.Id);
        b.AttachGatewayOrder("order_X");
        second.Orders.Add(b);
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Orders_with_no_gateway_id_yet_do_not_collide_on_the_filtered_unique_index()
    {
        var product = await _db.SeedProductAsync();
        await using var ctx = _db.CreateContext();
        ctx.Orders.Add(NewOrder("u1", "k1", product.Id));
        ctx.Orders.Add(NewOrder("u2", "k2", product.Id));

        await ctx.SaveChangesAsync();
        Assert.Equal(2, await ctx.Orders.CountAsync());
    }

    [Fact]
    public async Task Duplicate_SKUs_are_rejected()
    {
        var first = await _db.SeedProductAsync();
        await using var ctx = _db.CreateContext();
        ctx.Products.Add(new Product("Dup", first.SKU, "d", 1m, 1, "i", first.FranchiseId, first.CategoryId));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Order_totals_round_trip_through_the_database_exactly()
    {
        var product = await _db.SeedProductAsync();
        await using (var ctx = _db.CreateContext())
        {
            ctx.Orders.Add(new Order("u1", "k", new[] { new OrderLine(product.Id, "P", 3, 1999.99m) }));
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Equal(5999.97m, (await read.Orders.SingleAsync()).TotalAmount);
    }
}
