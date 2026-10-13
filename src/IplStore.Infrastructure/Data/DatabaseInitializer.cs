using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IplStore.Infrastructure.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// SQLite, or SQL Server before the first migration exists: EnsureCreated.
    /// SQL Server with migrations: Migrate. (Switching from EnsureCreated to Migrate needs a fresh database.)
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, bool seed, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IplStoreDbContext>();

        if (db.Database.ProviderName == IplStoreDbContext.SqliteProvider || !db.Database.GetMigrations().Any())
            await db.Database.EnsureCreatedAsync(ct);
        else
            await db.Database.MigrateAsync(ct);

        if (seed)
            await DbSeeder.SeedAsync(db, ct);
    }
}
