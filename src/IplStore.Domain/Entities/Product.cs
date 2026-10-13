using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Entities;

/// <remarks>
/// Stock is deliberately NOT mutated through this entity. Reserving/releasing stock is one atomic
/// "UPDATE ... SET Stock = Stock - @q WHERE Stock >= @q" (see IInventoryService), so a hot SKU never needs
/// read-modify-write retries. RowVersion still protects admin edits made through the tracked entity.
/// </remarks>
public class Product : Entity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string SKU { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public string ImageUrl { get; private set; } = default!;
    public Guid FranchiseId { get; private set; }
    public Guid CategoryId { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Product() { }

    public Product(string name, string sku, string description, decimal price, int stockQuantity,
                   string imageUrl, Guid franchiseId, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sku)) throw new ValidationException("Product SKU is required.");
        if (price <= 0) throw new ValidationException("Product price must be greater than zero.");
        if (stockQuantity < 0) throw new ValidationException("Stock quantity cannot be negative.");
        if (franchiseId == Guid.Empty) throw new ValidationException("Franchise is required.");
        if (categoryId == Guid.Empty) throw new ValidationException("Category is required.");

        Name = name.Trim();
        SKU = sku.Trim().ToUpperInvariant();
        Description = description ?? string.Empty;
        Price = price;
        StockQuantity = stockQuantity;
        ImageUrl = imageUrl ?? string.Empty;
        FranchiseId = franchiseId;
        CategoryId = categoryId;
    }
}
