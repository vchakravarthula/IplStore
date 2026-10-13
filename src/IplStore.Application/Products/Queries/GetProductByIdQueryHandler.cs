using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Products.Queries;

public sealed class GetProductByIdQueryHandler
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var row = await (
            from p in _db.Products.AsNoTracking()
            join f in _db.Franchises.AsNoTracking() on p.FranchiseId equals f.Id
            join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id
            where p.Id == id
            select new { Product = p, FranchiseName = f.Name, FranchiseCode = f.Code, CategoryName = c.Name })
            .FirstOrDefaultAsync(ct);

        if (row is null) throw new NotFoundException(nameof(Product), id);

        return new ProductDto(
            row.Product.Id, row.Product.Name, row.Product.SKU, row.Product.Description, row.Product.Price,
            row.Product.StockQuantity, row.Product.ImageUrl,
            row.Product.FranchiseId, row.FranchiseName, row.FranchiseCode,
            row.Product.CategoryId, row.CategoryName);
    }
}
