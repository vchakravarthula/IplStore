using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Data;

public class IplStoreDbContext : DbContext, IApplicationDbContext
{
    public const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    public IplStoreDbContext(DbContextOptions<IplStoreDbContext> options) : base(options) { }

    public DbSet<Franchise> Franchises => Set<Franchise>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IplStoreDbContext).Assembly);

        if (Database.ProviderName == SqliteProvider)
            ApplySqliteCompatibility(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    // SQLite (used for local runs and fast tests) has no rowversion and cannot ORDER BY / SUM decimals.
    // These two adjustments only apply to SQLite; SQL Server keeps native decimal(18,2) and rowversion.
    private static void ApplySqliteCompatibility(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            foreach (var property in entityType.GetProperties().ToList())
            {
                if (property.ClrType == typeof(decimal))
                    modelBuilder.Entity(entityType.ClrType).Property(property.Name).HasConversion(typeof(double));
            }

            if (entityType.FindProperty("RowVersion") is not null)
            {
                modelBuilder.Entity(entityType.ClrType).Property("RowVersion")
                    .ValueGeneratedNever()
                    .IsConcurrencyToken(false);
            }
        }
    }
}
