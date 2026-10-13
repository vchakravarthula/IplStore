using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.SKU).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.StockQuantity).IsRequired();

        // IsRowVersion() already implies a concurrency token. Guards admin edits made via the tracked entity.
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.SKU).IsUnique();
        builder.HasIndex(p => p.Name);                                // catalog search + default sort
        builder.HasIndex(p => new { p.FranchiseId, p.CategoryId });   // catalog filters

        builder.HasOne<Franchise>().WithMany().HasForeignKey(p => p.FranchiseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
