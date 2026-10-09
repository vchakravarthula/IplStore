using System;
using System.Collections.Generic;
using System.Text;
using IplStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Products.Queries.GetProducts
{
    public record ProductDto
        (
        Guid Id,
        string Name,
        string SKU,
        string Description,
        decimal Price,
        int StockQuantity,
        string ImageUrl,
        Guid FranchiseId,
        Guid CategoryId);

    public class GetProductsQueryHandler
    {
        private readonly IApplicationDbContext _context;

        public GetProductsQueryHandler(IApplicationDbContext dbContext)
        {
            _context = dbContext;
        }

        public async Task<List<ProductDto>> HandleAsync(Guid? franchiseId = null, Guid? categoryId = null, CancellationToken ct = default)
        {
            var query = _context.Products.AsNoTracking();

            if (franchiseId.HasValue)
                query = query.Where(p => p.FranchiseId == franchiseId.Value);

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            return await query.Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.SKU,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ImageUrl,
                p.FranchiseId,
                p.CategoryId
            )).ToListAsync(ct);
        }
    }

}
