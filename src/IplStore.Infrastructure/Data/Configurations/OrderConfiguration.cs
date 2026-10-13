using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.IdempotencyKey).HasMaxLength(Order.MaxIdempotencyKeyLength).IsRequired();
        builder.Property(o => o.ShortOrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.GatewayOrderId).HasMaxLength(64);
        builder.Property(o => o.PaymentTransactionId).HasMaxLength(64);

        // Detects "payment confirmed" racing "reservation expired" on the same order.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasIndex(o => o.ShortOrderNumber).IsUnique();

        // Idempotent checkout: one order per (user, key).
        builder.HasIndex(o => new { o.UserId, o.IdempotencyKey }).IsUnique();

        // Order history: WHERE UserId = @u ORDER BY CreatedAtUtc DESC.
        builder.HasIndex(o => new { o.UserId, o.CreatedAtUtc });

        // Expiry job: pending orders older than N minutes.
        builder.HasIndex(o => new { o.Status, o.CreatedAtUtc });

        // A payment id / gateway order id can be attached to at most one order.
        builder.HasIndex(o => o.PaymentTransactionId).IsUnique().HasFilter("[PaymentTransactionId] IS NOT NULL");
        builder.HasIndex(o => o.GatewayOrderId).IsUnique().HasFilter("[GatewayOrderId] IS NOT NULL");

        builder.HasMany(o => o.Items)
               .WithOne()
               .HasForeignKey(oi => oi.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items).Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
