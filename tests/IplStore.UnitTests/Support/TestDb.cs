using IplStore.Domain.Entities;
using IplStore.Infrastructure.Data;
using IplStore.Infrastructure.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IplStore.UnitTests.Support;

/// <summary>
/// A real EF Core context on in-memory SQLite (same model, same interceptor, real SQL incl. ExecuteUpdate and transactions).
/// Each test gets its own database; CreateContext() returns a fresh context on the SAME database,
/// which is how two "requests" are simulated.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public IplStoreDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IplStoreDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new ConvertDomainEventsToOutboxMessagesInterceptor())
            .Options;
        return new IplStoreDbContext(options);
    }

    /// <summary>Adds one franchise, one category and one product with the given stock.</summary>
    public async Task<Product> SeedProductAsync(int stock = 10, decimal price = 100m, string name = "CSK Jersey",
                                                string franchise = "Chennai Super Kings", string code = "CSK", string category = "Jersey")
    {
        await using var db = CreateContext();
        var f = await db.Franchises.FirstOrDefaultAsync(x => x.Code == code) ?? new Franchise(franchise, code, "");
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Name == category) ?? new Category(category, category.ToLowerInvariant());
        if (db.Entry(f).State == EntityState.Detached) db.Franchises.Add(f);
        if (db.Entry(c).State == EntityState.Detached) db.Categories.Add(c);

        var product = new Product(name, $"{code}-{Guid.NewGuid():N}"[..20], "desc", price, stock, "img", f.Id, c.Id);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public async Task<int> StockOfAsync(Guid productId)
    {
        await using var db = CreateContext();
        return await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync();
    }

    public void Dispose() => _connection.Dispose();
}
