using IplStore.Api.Extensions;
using IplStore.Application.Orders.Commands;
using IplStore.Application.Orders.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

public sealed record ConfirmPaymentRequest(string GatewayOrderId, string GatewayPaymentId, string Signature);

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CheckoutCommandHandler _checkout;
    private readonly ConfirmPaymentCommandHandler _confirmPayment;
    private readonly GetOrdersQueryHandler _orders;

    public OrdersController(CheckoutCommandHandler checkout, ConfirmPaymentCommandHandler confirmPayment, GetOrdersQueryHandler orders)
    {
        _checkout = checkout;
        _confirmPayment = confirmPayment;
        _orders = orders;
    }

    /// <summary>Turns the caller's cart into an order. Send a unique Idempotency-Key per checkout attempt; retries are safe.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var response = await _checkout.HandleAsync(new CheckoutCommand(User.GetUserId(), idempotencyKey ?? string.Empty), ct);
        return Ok(response);
    }

    [HttpPost("{orderId:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid orderId, [FromBody] ConfirmPaymentRequest request, CancellationToken ct)
    {
        var status = await _confirmPayment.HandleAsync(
            new ConfirmPaymentCommand(User.GetUserId(), orderId, request.GatewayOrderId, request.GatewayPaymentId, request.Signature), ct);
        return Ok(new { orderId, status });
    }

    /// <summary>Order history for the caller, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _orders.ListAsync(User.GetUserId(), page, pageSize, ct));

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken ct) =>
        Ok(await _orders.GetByIdAsync(User.GetUserId(), orderId, ct));
}
