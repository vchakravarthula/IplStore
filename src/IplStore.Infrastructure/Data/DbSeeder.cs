using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Data;

/// <summary>Demo catalog: 10 franchises x 4 product types. Idempotent (does nothing if data exists).</summary>
public static class DbSeeder
{
    private static readonly (string Name, string Code)[] Franchises =
    {
        ("Chennai Super Kings", "CSK"), ("Mumbai Indians", "MI"), ("Royal Challengers Bengaluru", "RCB"),
        ("Kolkata Knight Riders", "KKR"), ("Delhi Capitals", "DC"), ("Sunrisers Hyderabad", "SRH"),
        ("Rajasthan Royals", "RR"), ("Punjab Kings", "PBKS"), ("Gujarat Titans", "GT"), ("Lucknow Super Giants", "LSG")
    };

    // (name, slug, price, stock): autographed photos are deliberately scarce (good for oversell demos).
    private static readonly (string Name, string Slug, decimal Price, int Stock)[] Categories =
    {
        ("Jersey", "jersey", 1999m, 100), ("Cap", "cap", 599m, 150),
        ("Flag", "flag", 349m, 200), ("Autographed Photo", "autographed-photo", 2999m, 5)
    };

    public static async Task SeedAsync(IplStoreDbContext db, CancellationToken ct = default)
    {
        if (await db.Franchises.AnyAsync(ct)) return;

        var franchises = Franchises.Select(f => new Franchise(f.Name, f.Code, $"/images/{f.Code.ToLowerInvariant()}-logo.png")).ToList();
        var categories = Categories.Select(c => new Category(c.Name, c.Slug)).ToList();
        db.Franchises.AddRange(franchises);
        db.Categories.AddRange(categories);

        foreach (var franchise in franchises)
        {
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var spec = Categories[i];
                db.Products.Add(new Product(
                    name: $"{franchise.Name} {spec.Name}",
                    sku: $"{franchise.Code}-{spec.Slug}",
                    description: $"Official {franchise.Name} {spec.Name.ToLowerInvariant()}.",
                    price: spec.Price,
                    stockQuantity: spec.Stock,
                    imageUrl: $"/images/{franchise.Code.ToLowerInvariant()}-{spec.Slug}.png",
                    franchiseId: franchise.Id,
                    categoryId: category.Id));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
