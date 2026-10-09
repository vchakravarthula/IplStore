using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Infrastructure.Data.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.UserId).IsRequired();
            builder.Property(o => o.ShortOrderNumber).HasMaxLength(30).IsRequired();
            builder.HasIndex(o => o.ShortOrderNumber).IsUnique();

            builder.Property(o => o.TotalAmount).HasPrecision(18, 2).IsRequired();

            builder.Property(o => o.Status)
                   .HasConversion<string>()
                   .HasMaxLength(30);

            builder.HasMany(o => o.Items)
                   .WithOne()
                   .HasForeignKey(oi => oi.OrderId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Access private backing collection
            builder.Navigation(o => o.Items).Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
