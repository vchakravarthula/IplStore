using IplStore.Application.Products.Queries;
using IplStore.Domain.Exceptions;
using IplStore.UnitTests.Support;

namespace IplStore.UnitTests.Application;

public sealed class GetProductsQueryHandlerTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private GetProductsQueryHandler Handler() => new(_db.CreateContext());

    private async Task SeedCatalogAsync()
    {
        await _db.SeedProductAsync(price: 1999m, name: "Chennai Super Kings Jersey", franchise: "Chennai Super Kings", code: "CSK", category: "Jersey");
        await _db.SeedProductAsync(price: 599m, name: "Chennai Super Kings Cap", franchise: "Chennai Super Kings", code: "CSK", category: "Cap");
        await _db.SeedProductAsync(price: 1899m, name: "Mumbai Indians Jersey", franchise: "Mumbai Indians", code: "MI", category: "Jersey");
        await _db.SeedProductAsync(price: 349m, name: "Mumbai Indians Flag", franchise: "Mumbai Indians", code: "MI", category: "Flag");
    }

    [Fact]
    public async Task Search_matches_product_name()
    {
        await SeedCatalogAsync();
        var result = await Handler().HandleAsync(new GetProductsQuery(Search: "Flag"));
        Assert.Equal("Mumbai Indians Flag", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task Search_matches_franchise_code_and_is_case_insensitive()
    {
        await SeedCatalogAsync();
        var result = await Handler().HandleAsync(new GetProductsQuery(Search: "csk"));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_matches_category_name()
    {
        await SeedCatalogAsync();
        var result = await Handler().HandleAsync(new GetProductsQuery(Search: "jersey"));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Wildcard_characters_typed_by_the_user_are_treated_literally()
    {
        await SeedCatalogAsync();
        var result = await Handler().HandleAsync(new GetProductsQuery(Search: "%"));
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Filters_combine()
    {
        await SeedCatalogAsync();
        var result = await Handler().HandleAsync(new GetProductsQuery(Search: "jersey", MaxPrice: 1900m));
        Assert.Equal("Mumbai Indians Jersey", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task Sort_by_price_ascending_and_descending()
    {
        await SeedCatalogAsync();
        var asc = await Handler().HandleAsync(new GetProductsQuery(SortBy: "price"));
        var desc = await Handler().HandleAsync(new GetProductsQuery(SortBy: "price", Descending: true));

        Assert.Equal(349m, asc.Items.First().Price);
        Assert.Equal(1999m, desc.Items.First().Price);
    }

    [Fact]
    public async Task Paging_is_bounded_and_stable()
    {
        await SeedCatalogAsync();
        var page1 = await Handler().HandleAsync(new GetProductsQuery(Page: 1, PageSize: 3));
        var page2 = await Handler().HandleAsync(new GetProductsQuery(Page: 2, PageSize: 3));
        var huge = await Handler().HandleAsync(new GetProductsQuery(PageSize: 10_000));

        Assert.Equal(3, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(GetProductsQueryHandler.MaxPageSize, huge.PageSize);   // a caller can never request an unbounded page
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
    }

    [Fact]
    public async Task Product_by_id_includes_franchise_and_category_names_and_404s_when_unknown()
    {
        var product = await _db.SeedProductAsync(name: "X", franchise: "Gujarat Titans", code: "GT", category: "Cap");

        var dto = await new GetProductByIdQueryHandler(_db.CreateContext()).HandleAsync(product.Id);

        Assert.Equal("Gujarat Titans", dto.FranchiseName);
        Assert.Equal("Cap", dto.CategoryName);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetProductByIdQueryHandler(_db.CreateContext()).HandleAsync(Guid.NewGuid()));
    }
}
