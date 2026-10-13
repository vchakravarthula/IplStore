using IplStore.Application.Products.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace IplStore.Api.Controllers;

// The catalog is public: browsing needs no login (and anonymous responses can be cached at the edge).
[ApiController]
[AllowAnonymous]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly GetProductsQueryHandler _list;
    private readonly GetProductByIdQueryHandler _byId;

    public ProductsController(GetProductsQueryHandler list, GetProductByIdQueryHandler byId)
    {
        _list = list;
        _byId = byId;
    }

    /// <summary>Search by name/SKU/franchise/category text, filter, sort (name|price) and page.</summary>
    [HttpGet]
    [OutputCache(PolicyName = "Catalog")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] Guid? franchiseId,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? sortBy,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _list.HandleAsync(
            new GetProductsQuery(search, franchiseId, categoryId, minPrice, maxPrice, sortBy, desc, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = "Catalog")]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken ct) =>
        Ok(await _byId.HandleAsync(id, ct));
}
