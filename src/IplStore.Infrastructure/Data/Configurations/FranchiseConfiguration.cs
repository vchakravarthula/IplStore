using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Data.Configurations;

public class FranchiseConfiguration : IEntityTypeConfiguration<Franchise>
{
    public void Configure(EntityTypeBuilder<Franchise> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Code).HasMaxLength(10).IsRequired();
        builder.Property(f => f.LogoUrl).HasMaxLength(500);
        builder.HasIndex(f => f.Code).IsUnique();
    }
}
