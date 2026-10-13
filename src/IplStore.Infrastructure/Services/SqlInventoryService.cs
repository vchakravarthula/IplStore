using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Services;

/// <summary>
/// Stock movements as single conditional UPDATE statements:
///   UPDATE Products SET StockQuantity = StockQuantity - @q WHERE Id = @id AND StockQuantity >= @q
/// No read-modify-write, no retry storms on a hot SKU, and it can never oversell. rows-affected = 0 means "not enough".
/// Lines are merged per product and processed in ProductId order so concurrent multi-item orders always lock rows in
/// the same order (no deadlocks).
/// </summary>
public sealed class SqlInventoryService : IInventoryService
{
    private readonly IplStoreDbContext _db;

    public SqlInventoryService(IplStoreDbContext db) => _db = db;

    public async Task ReserveAsync(IReadOnlyCollection<StockLine> lines, CancellationToken ct = default)
    {
        foreach (var line in Normalize(lines))
        {
            var productId = line.ProductId;
            var quantity = line.Quantity;

            var updated = await _db.Products
                .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity), ct);

            if (updated == 1) continue;

            // Zero rows: unknown product, or not enough stock. Distinguish for a precise error.
            var available = await _db.Products.AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => (int?)p.StockQuantity)
                .FirstOrDefaultAsync(ct);

            if (available is null) throw new NotFoundException(nameof(Product), productId);
            throw new InsufficientStockException(productId.ToString(), quantity, available.Value);
        }
    }

    public async Task ReleaseAsync(IReadOnlyCollection<StockLine> lines, CancellationToken ct = default)
    {
        foreach (var line in Normalize(lines))
        {
            var productId = line.ProductId;
            var quantity = line.Quantity;

            var updated = await _db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity), ct);

            if (updated == 0) throw new NotFoundException(nameof(Product), productId);
        }
    }

    private static List<StockLine> Normalize(IReadOnlyCollection<StockLine> lines)
    {
        if (lines.Any(l => l.Quantity <= 0))
            throw new ValidationException("Stock quantity must be greater than zero.");

        return lines
            .GroupBy(l => l.ProductId)
            .Select(g => new StockLine(g.Key, g.Sum(l => l.Quantity)))
            .OrderBy(l => l.ProductId)
            .ToList();
    }
}
