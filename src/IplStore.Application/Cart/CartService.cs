using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Cart;

public sealed record CartItemDto(Guid ProductId, string Name, string ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal, int AvailableStock);

public sealed record CartDto(IReadOnlyList<CartItemDto> Items, decimal Total);

/// <summary>
/// The cart store only holds (productId, quantity). Names and prices are always read from the catalog,
/// so the client can never influence a price and a catalog price change shows up immediately.
/// </summary>
public sealed class CartService
{
    private readonly IApplicationDbContext _db;
    private readonly ICartRepository _carts;

    public CartService(IApplicationDbContext db, ICartRepository carts)
    {
        _db = db;
        _carts = carts;
    }

    public async Task<CartDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var lines = await _carts.GetAsync(userId, ct);
        if (lines.Count == 0) return new CartDto(Array.Empty<CartItemDto>(), 0m);

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // A product deleted after being carted is simply skipped.
        var items = lines
            .Where(l => products.ContainsKey(l.ProductId))
            .Select(l =>
            {
                var p = products[l.ProductId];
                return new CartItemDto(p.Id, p.Name, p.ImageUrl, p.Price, l.Quantity, p.Price * l.Quantity, p.StockQuantity);
            })
            .OrderBy(i => i.Name)
            .ToList();

        return new CartDto(items, items.Sum(i => i.LineTotal));
    }

    public async Task<CartDto> AddAsync(string userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        ValidateQuantity(quantity);
        var product = await FindProductAsync(productId, ct);

        if (product.StockQuantity < quantity)
            throw new InsufficientStockException(product.SKU, quantity, product.StockQuantity);

        var newQuantity = await _carts.AddAsync(userId, productId, quantity, CartRules.MaxQuantityPerItem, ct);
        if (newQuantity is null)
            throw new ValidationException($"You can buy at most {CartRules.MaxQuantityPerItem} units of a product per order.");

        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> SetQuantityAsync(string userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        ValidateQuantity(quantity);
        var product = await FindProductAsync(productId, ct);

        if (product.StockQuantity < quantity)
            throw new InsufficientStockException(product.SKU, quantity, product.StockQuantity);

        await _carts.SetQuantityAsync(userId, productId, quantity, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> RemoveAsync(string userId, Guid productId, CancellationToken ct = default)
    {
        await _carts.RemoveAsync(userId, productId, ct);
        return await GetAsync(userId, ct);
    }

    public Task ClearAsync(string userId, CancellationToken ct = default) => _carts.ClearAsync(userId, ct);

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 1 || quantity > CartRules.MaxQuantityPerItem)
            throw new ValidationException($"Quantity must be between 1 and {CartRules.MaxQuantityPerItem}.");
    }

    private async Task<Product> FindProductAsync(Guid productId, CancellationToken ct) =>
        await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
        ?? throw new NotFoundException(nameof(Product), productId);
}
