using IplStore.Application.Common.Interfaces;
using IplStore.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Products.Queries;

public sealed record GetProductsQuery(
    string? Search = null,
    Guid? FranchiseId = null,
    Guid? CategoryId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20);

public sealed class GetProductsQueryHandler
{
    public const int MaxPageSize = 50;
    private const string LikeEscape = "\\";

    private readonly IApplicationDbContext _db;

    public GetProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> HandleAsync(GetProductsQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);   // bounded: a catalog request can never be unbounded

        var query =
            from p in _db.Products.AsNoTracking()
            join f in _db.Franchises.AsNoTracking() on p.FranchiseId equals f.Id
            join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id
            select new { Product = p, FranchiseName = f.Name, FranchiseCode = f.Code, CategoryName = c.Name };

        if (q.FranchiseId.HasValue) query = query.Where(x => x.Product.FranchiseId == q.FranchiseId.Value);
        if (q.CategoryId.HasValue) query = query.Where(x => x.Product.CategoryId == q.CategoryId.Value);
        if (q.MinPrice.HasValue) query = query.Where(x => x.Product.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(x => x.Product.Price <= q.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // Search across name, SKU, franchise (name or code) and category. Wildcards typed by the user are escaped.
            var pattern = $"%{EscapeLike(q.Search.Trim())}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Product.Name, pattern, LikeEscape) ||
                EF.Functions.Like(x.Product.SKU, pattern, LikeEscape) ||
                EF.Functions.Like(x.FranchiseName, pattern, LikeEscape) ||
                EF.Functions.Like(x.FranchiseCode, pattern, LikeEscape) ||
                EF.Functions.Like(x.CategoryName, pattern, LikeEscape));
        }

        var total = await query.CountAsync(ct);

        // Id is the tie-breaker so paging is stable when many rows share a price or name.
        var ordered = (q.SortBy?.ToLowerInvariant(), q.Descending) switch
        {
            ("price", false) => query.OrderBy(x => x.Product.Price).ThenBy(x => x.Product.Id),
            ("price", true) => query.OrderByDescending(x => x.Product.Price).ThenBy(x => x.Product.Id),
            (_, true) => query.OrderByDescending(x => x.Product.Name).ThenBy(x => x.Product.Id),
            _ => query.OrderBy(x => x.Product.Name).ThenBy(x => x.Product.Id)
        };

        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(x => new ProductDto(
            x.Product.Id, x.Product.Name, x.Product.SKU, x.Product.Description, x.Product.Price,
            x.Product.StockQuantity, x.Product.ImageUrl,
            x.Product.FranchiseId, x.FranchiseName, x.FranchiseCode,
            x.Product.CategoryId, x.CategoryName)).ToList();

        return new PagedResult<ProductDto>(items, page, pageSize, total);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
