using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Orders.Commands;

public sealed record CheckoutCommand(string UserId, string IdempotencyKey);

public sealed record CheckoutResponse(
    Guid OrderId,
    string ShortOrderNumber,
    decimal TotalAmount,
    string Currency,
    string Status,
    string? GatewayOrderId);

/// <summary>
/// Flow:  find-by-idempotency-key  ->  reuse the user's open order if the cart is unchanged  ->
///        [tx: release any outdated open order + reserve stock (atomic UPDATE) + insert order + outbox row]  ->
///        create gateway order (outside the tx: never hold DB locks across a network call).
/// The cart is NOT cleared here: it is cleared when the payment succeeds (ConfirmPaymentCommandHandler),
/// so a failed or abandoned payment leaves the customer's cart intact.
/// There is at most ONE open (unpaid) order per user, so retrying checkout can never reserve stock twice.
/// If the gateway call fails the reservation is compensated (order Failed, stock released).
/// </summary>
public sealed class CheckoutCommandHandler
{
    public const string Currency = "INR";   // server-side constant: the client never chooses the currency

    private readonly IApplicationDbContext _db;
    private readonly ICartRepository _carts;
    private readonly IInventoryService _inventory;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<CheckoutCommandHandler> _logger;

    public CheckoutCommandHandler(
        IApplicationDbContext db,
        ICartRepository carts,
        IInventoryService inventory,
        IPaymentGateway gateway,
        ILogger<CheckoutCommandHandler> logger)
    {
        _db = db;
        _carts = carts;
        _inventory = inventory;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<CheckoutResponse> HandleAsync(CheckoutCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.UserId))
            throw new ValidationException("UserId is required.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > Order.MaxIdempotencyKeyLength)
            throw new ValidationException($"Idempotency-Key header is required (max {Order.MaxIdempotencyKeyLength} characters).");

        var order = await PlaceOrderAsync(command, ct);

        // New order, or an earlier attempt committed the order but crashed before reaching the gateway.
        if (order.GatewayOrderId is null && order.Status == OrderStatus.PendingPayment)
            await CreateGatewayOrderAsync(order, ct);

        return ToResponse(order);
    }

    private async Task<Order> PlaceOrderAsync(CheckoutCommand command, CancellationToken ct)
    {
        var existing = await FindByKeyAsync(command, ct);
        if (existing is not null) return existing;   // idempotent replay

        var lines = await _carts.GetAsync(command.UserId, ct);
        if (lines.Count == 0) throw new ValidationException("Cart is empty.");

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Prices come from the catalog, never from the cart or the client.
        var orderLines = new List<OrderLine>();
        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException(nameof(Product), line.ProductId);
            orderLines.Add(new OrderLine(product.Id, product.Name, line.Quantity, product.Price));
        }

        var newTotal = orderLines.Sum(l => l.Quantity * l.UnitPrice);

        // One open (unpaid) order per user. Same cart at the same prices: reuse it (a retry, or the user left and came back),
        // so stock is never reserved twice. Different cart: that order is released in the same transaction as the new reservation.
        var openOrders = await _db.Orders.Include(o => o.Items)
            .Where(o => o.UserId == command.UserId && o.Status == OrderStatus.PendingPayment)
            .ToListAsync(ct);

        var reusable = openOrders.FirstOrDefault(o => o.TotalAmount == newTotal && SameLines(o, orderLines));
        if (reusable is not null) return reusable;

        var order = new Order(command.UserId, command.IdempotencyKey, orderLines);
        var stock = lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToList();

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                foreach (var outdated in openOrders)
                {
                    outdated.Expire();
                    await _inventory.ReleaseAsync(outdated.Items.Select(i => new StockLine(i.ProductId, i.Quantity)).ToList(), ct);
                }

                await _inventory.ReserveAsync(stock, ct);   // throws InsufficientStockException -> tx rolls back (old order too)
                _db.Orders.Add(order);
                await _db.SaveChangesAsync(ct);             // order + OrderPlaced outbox row, same transaction
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateException)
        {
            // Two concurrent requests with the same key: the unique (UserId, IdempotencyKey) index let one win.
            // The loser's transaction (including its stock reservation) was rolled back; return the winner.
            _db.ChangeTracker.Clear();
            var winner = await FindByKeyAsync(command, ct);
            if (winner is null) throw;
            return winner;
        }

        return order;
    }

    private async Task CreateGatewayOrderAsync(Order order, CancellationToken ct)
    {
        PaymentOrderResult result;
        try
        {
            result = await _gateway.CreateOrderAsync(
                new PaymentOrderRequest(order.TotalAmount, Currency, order.ShortOrderNumber), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway order creation failed for order {OrderNumber}; releasing reserved stock.", order.ShortOrderNumber);
            // CancellationToken.None: compensation must complete even if the caller disconnected.
            await OrderLifecycle.TransitionAndReleaseStockAsync(_db, _inventory, order, o => o.MarkAsFailed(), CancellationToken.None);
            throw;
        }

        order.AttachGatewayOrder(result.GatewayOrderId);
        await _db.SaveChangesAsync(ct);
    }

    private static bool SameLines(Order order, IReadOnlyCollection<OrderLine> lines) =>
        order.Items.Count == lines.Count &&
        lines.All(l => order.Items.Any(i => i.ProductId == l.ProductId && i.Quantity == l.Quantity));

    private Task<Order?> FindByKeyAsync(CheckoutCommand command, CancellationToken ct) =>
        _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == command.UserId && o.IdempotencyKey == command.IdempotencyKey, ct);

    private static CheckoutResponse ToResponse(Order order) => new(
        order.Id, order.ShortOrderNumber, order.TotalAmount, Currency, order.Status.ToString(), order.GatewayOrderId);
}
