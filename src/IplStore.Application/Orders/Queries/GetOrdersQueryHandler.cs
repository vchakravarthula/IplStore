using IplStore.Application.Common.Interfaces;
using IplStore.Application.Common.Models;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Orders.Queries;

public sealed record OrderSummaryDto(Guid Id, string ShortOrderNumber, string Status, decimal TotalAmount, int ItemCount, DateTime CreatedAtUtc);

public sealed record OrderItemDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderDetailDto(
    Guid Id,
    string ShortOrderNumber,
    string Status,
    decimal TotalAmount,
    string? GatewayOrderId,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items);

public sealed class GetOrdersQueryHandler
{
    public const int MaxPageSize = 50;

    private readonly IApplicationDbContext _db;

    public GetOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    /// <summary>Order history for ONE user, newest first. The user id always comes from the caller's token.</summary>
    public async Task<PagedResult<OrderSummaryDto>> ListAsync(string userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.Id, o.ShortOrderNumber, o.Status, o.TotalAmount, ItemCount = o.Items.Count, o.CreatedAtUtc })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new OrderSummaryDto(r.Id, r.ShortOrderNumber, r.Status.ToString(), r.TotalAmount, r.ItemCount, r.CreatedAtUtc))
            .ToList();

        return new PagedResult<OrderSummaryDto>(items, page, pageSize, total);
    }

    public async Task<OrderDetailDto> GetByIdAsync(string userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        return new OrderDetailDto(
            order.Id, order.ShortOrderNumber, order.Status.ToString(), order.TotalAmount, order.GatewayOrderId, order.CreatedAtUtc,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.TotalPrice)).ToList());
    }
}
