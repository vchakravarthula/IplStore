using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using IplStore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Orders.Commands;

public sealed record ConfirmPaymentCommand(
    string UserId,
    Guid OrderId,
    string GatewayOrderId,
    string GatewayPaymentId,
    string Signature);

public sealed class ConfirmPaymentCommandHandler
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ICartRepository _carts;
    private readonly ILogger<ConfirmPaymentCommandHandler> _logger;

    public ConfirmPaymentCommandHandler(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        ICartRepository carts,
        ILogger<ConfirmPaymentCommandHandler> logger)
    {
        _db = db;
        _gateway = gateway;
        _carts = carts;
        _logger = logger;
    }

    /// <returns>The resulting order status. Safe to call repeatedly: confirming the same payment twice is a no-op.</returns>
    public async Task<string> HandleAsync(ConfirmPaymentCommand command, CancellationToken ct = default)
    {
        // Scoped to the caller: someone else's order id is indistinguishable from a missing one.
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == command.OrderId && o.UserId == command.UserId, ct)
                    ?? throw new NotFoundException(nameof(Order), command.OrderId);

        if (order.GatewayOrderId != command.GatewayOrderId)
            throw new ValidationException("The payment does not belong to this order.");

        var valid = _gateway.VerifySignature(
            new PaymentVerificationRequest(command.GatewayOrderId, command.GatewayPaymentId, command.Signature));
        if (!valid)
            throw new ValidationException("Invalid payment signature.");

        var alreadyPaid = order.Status == OrderStatus.Paid;
        order.MarkAsPaid(command.GatewayPaymentId);   // Conflict if the order already expired

        try
        {
            await _db.SaveChangesAsync(ct);           // RowVersion: loses cleanly against a racing expiry job (409)
        }
        catch (DbUpdateConcurrencyException)
        {
            throw;                                    // already mapped to 409 by the exception handler
        }
        catch (DbUpdateException)
        {
            // Unique index IX_Orders_PaymentTransactionId: one payment can only settle one order.
            _db.ChangeTracker.Clear();
            var alreadyUsed = await _db.Orders.AsNoTracking()
                .AnyAsync(o => o.PaymentTransactionId == command.GatewayPaymentId && o.Id != command.OrderId, ct);
            if (alreadyUsed)
                throw new ConflictException("This payment has already been applied to another order.");
            throw;
        }

        // The cart is cleared only now that the money is confirmed. A replayed confirmation must not touch it again.
        if (!alreadyPaid)
            await RemovePaidItemsFromCartAsync(order, ct);

        return order.Status.ToString();
    }

    /// <summary>Best effort: the order is already paid, so a cart-store outage must not turn a successful payment into an error.</summary>
    private async Task RemovePaidItemsFromCartAsync(Order order, CancellationToken ct)
    {
        try
        {
            foreach (var item in order.Items)
                await _carts.RemoveAsync(order.UserId, item.ProductId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Order {OrderNumber} is paid but its items could not be removed from the cart.", order.ShortOrderNumber);
        }
    }
}
