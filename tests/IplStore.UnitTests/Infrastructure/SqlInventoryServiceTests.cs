using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Services;
using IplStore.UnitTests.Support;

namespace IplStore.UnitTests.Infrastructure;

public sealed class SqlInventoryServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private SqlInventoryService NewService() => new(_db.CreateContext());

    [Fact]
    public async Task Reserve_decrements_stock()
    {
        var product = await _db.SeedProductAsync(stock: 10);

        await NewService().ReserveAsync(new[] { new StockLine(product.Id, 4) });

        Assert.Equal(6, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Reserving_exactly_the_remaining_stock_succeeds_and_one_more_fails_without_going_negative()
    {
        var product = await _db.SeedProductAsync(stock: 3);

        await NewService().ReserveAsync(new[] { new StockLine(product.Id, 3) });
        var ex = await Assert.ThrowsAsync<InsufficientStockException>(
            () => NewService().ReserveAsync(new[] { new StockLine(product.Id, 1) }));

        Assert.Equal(0, ex.AvailableQuantity);
        Assert.Equal(0, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Unknown_product_is_reported_as_not_found_not_as_out_of_stock()
        => await Assert.ThrowsAsync<NotFoundException>(
            () => NewService().ReserveAsync(new[] { new StockLine(Guid.NewGuid(), 1) }));

    [Fact]
    public async Task Duplicate_lines_for_one_product_are_merged_before_the_stock_check()
    {
        var product = await _db.SeedProductAsync(stock: 5);

        // 3 + 3 = 6 > 5: must fail as a whole, not pass line by line.
        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            NewService().ReserveAsync(new[] { new StockLine(product.Id, 3), new StockLine(product.Id, 3) }));
    }

    [Fact]
    public async Task Non_positive_quantities_are_rejected()
    {
        var product = await _db.SeedProductAsync();
        await Assert.ThrowsAsync<ValidationException>(() => NewService().ReserveAsync(new[] { new StockLine(product.Id, 0) }));
        await Assert.ThrowsAsync<ValidationException>(() => NewService().ReleaseAsync(new[] { new StockLine(product.Id, -2) }));
    }

    [Fact]
    public async Task Release_gives_stock_back()
    {
        var product = await _db.SeedProductAsync(stock: 10);
        await NewService().ReserveAsync(new[] { new StockLine(product.Id, 4) });

        await NewService().ReleaseAsync(new[] { new StockLine(product.Id, 4) });

        Assert.Equal(10, await _db.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task A_failed_line_inside_a_transaction_rolls_back_the_lines_reserved_before_it()
    {
        var plenty = await _db.SeedProductAsync(stock: 10, name: "Plenty", code: "AAA");
        var scarce = await _db.SeedProductAsync(stock: 1, name: "Scarce", code: "ZZZ");

        await using (var ctx = _db.CreateContext())
        {
            var service = new SqlInventoryService(ctx);
            await using var tx = await ctx.Database.BeginTransactionAsync();
            await Assert.ThrowsAsync<InsufficientStockException>(() =>
                service.ReserveAsync(new[] { new StockLine(plenty.Id, 2), new StockLine(scarce.Id, 5) }));
            // tx disposed without commit == rollback, exactly what CheckoutCommandHandler relies on
        }

        Assert.Equal(10, await _db.StockOfAsync(plenty.Id));
        Assert.Equal(1, await _db.StockOfAsync(scarce.Id));
    }
}
