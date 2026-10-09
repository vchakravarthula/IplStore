using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
            builder.Property(p => p.SKU).HasMaxLength(50).IsRequired();
            builder.HasIndex(p => p.SKU).IsUnique();

            builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
            builder.Property(p => p.StockQuantity).IsRequired();

            // SQL Server RowVersion for Optimistic Concurrency Control during peak spike
            builder.Property(p => p.RowVersion)
                   .IsRowVersion()
                   .IsConcurrencyToken();

            builder.HasOne<Franchise>()
                   .WithMany()
                   .HasForeignKey(p => p.FranchiseId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Category>()
                   .WithMany()
                   .HasForeignKey(p => p.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
