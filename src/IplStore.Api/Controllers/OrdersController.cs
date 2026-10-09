using IplStore.Api.Extensions;
using IplStore.Application.Orders.Commands.Checkouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IplStore.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly CheckoutCommandHandler _checkoutHandler;

        public OrdersController(CheckoutCommandHandler checkoutHandler)
        {
            _checkoutHandler = checkoutHandler;
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(CancellationToken ct)
        {
            // Extract userId securely from JWT 'sub' claim
            var userId = User.GetUserId();

            // Pass authenticated userId to the handler (handler fetches cart items based on this userId)
            var command = new CheckoutCommand(userId);
            var response = await _checkoutHandler.HandleAsync(command, ct);
            return Ok(response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrderById(Guid id, CancellationToken ct)
        {
            // Add authorization check ensuring order belongs to User.GetUserId()
            return Ok();
        }
    }
}
