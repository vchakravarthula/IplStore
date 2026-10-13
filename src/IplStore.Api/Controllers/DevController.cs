using System.Security.Cryptography;
using System.Text;
using IplStore.Api.Extensions;
using IplStore.Application.Orders.Commands;
using IplStore.Application.Orders.Queries;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IplStore.Api.Controllers;

/// <summary>
/// DEVELOPMENT-ONLY demo insurance: pays the caller's own order without the Razorpay popup (for a flaky network).
/// It goes through the SAME confirm handler and signature verification as a real payment, and returns 404 in any other environment.
/// </summary>
[ApiController]
[Authorize]
[Route("api/dev")]
public sealed class DevController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly RazorpaySettings _razorpay;
    private readonly GetOrdersQueryHandler _orders;
    private readonly ConfirmPaymentCommandHandler _confirm;

    public DevController(
        IWebHostEnvironment environment,
        IOptions<RazorpaySettings> razorpay,
        GetOrdersQueryHandler orders,
        ConfirmPaymentCommandHandler confirm)
    {
        _environment = environment;
        _razorpay = razorpay.Value;
        _orders = orders;
        _confirm = confirm;
    }

    [HttpPost("orders/{orderId:guid}/simulate-payment")]
    public async Task<IActionResult> SimulatePayment(Guid orderId, CancellationToken ct)
    {
        if (!_environment.IsDevelopment()) return NotFound();

        var userId = User.GetUserId();
        var order = await _orders.GetByIdAsync(userId, orderId, ct);   // ownership enforced: someone else's order is a 404
        if (order.GatewayOrderId is null)
            throw new ValidationException("The order has no payment session yet.");

        var paymentId = $"pay_sim_{Guid.NewGuid():N}"[..22];
        var payload = Encoding.UTF8.GetBytes($"{order.GatewayOrderId}|{paymentId}");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_razorpay.KeySecret), payload)).ToLowerInvariant();

        var status = await _confirm.HandleAsync(new ConfirmPaymentCommand(userId, orderId, order.GatewayOrderId, paymentId, signature), ct);
        return Ok(new { orderId, status, paymentId, simulated = true });
    }
}
