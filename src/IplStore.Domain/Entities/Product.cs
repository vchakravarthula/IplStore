using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
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

        // Concurrency Token for SQL Server rowversion
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

        private Product() { }

        public Product(string name, string sku, string description, decimal price, int stockQuantity, string imageUrl, Guid franchiseId, Guid categoryId)
        {
            Name = name;
            SKU = sku;
            Description = description;
            Price = price;
            StockQuantity = stockQuantity;
            ImageUrl = imageUrl;
            FranchiseId = franchiseId;
            CategoryId = categoryId;
        }

        public void DeductStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

            if (StockQuantity < quantity)
                throw new InvalidOperationException($"Insufficient stock for SKU {SKU}. Requested: {quantity}, Available: {StockQuantity}");

            StockQuantity -= quantity;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void Restock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

            StockQuantity += quantity;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
