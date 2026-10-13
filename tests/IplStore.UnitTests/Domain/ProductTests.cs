using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;

namespace IplStore.UnitTests.Domain;

public class ProductTests
{
    private static readonly Guid F = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Valid_product_normalises_name_and_sku()
    {
        var p = new Product("  CSK Jersey ", "csk-jsy-001", "d", 1999m, 10, "u", F, C);
        Assert.Equal("CSK Jersey", p.Name);
        Assert.Equal("CSK-JSY-001", p.SKU);
    }

    [Theory]
    [InlineData("", "SKU", 10, 1)]
    [InlineData("N", "", 10, 1)]
    [InlineData("N", "SKU", 0, 1)]
    [InlineData("N", "SKU", -5, 1)]
    [InlineData("N", "SKU", 10, -1)]
    public void Invalid_input_is_rejected_before_construction(string name, string sku, int price, int stock)
        => Assert.Throws<ValidationException>(() => new Product(name, sku, "d", price, stock, "u", F, C));

    [Fact]
    public void Franchise_and_category_are_required()
    {
        Assert.Throws<ValidationException>(() => new Product("N", "S", "d", 1m, 1, "u", Guid.Empty, C));
        Assert.Throws<ValidationException>(() => new Product("N", "S", "d", 1m, 1, "u", F, Guid.Empty));
    }
}
