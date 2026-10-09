using IplStore.Api.Extensions;
using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly ICartRepository _cartRepository;

        public CartController(ICartRepository cartRepository)
        {
            _cartRepository = cartRepository;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCart( CancellationToken ct)
        {
            var userId = User.GetUserId();
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is null)
            {
                return Ok(new Cart(userId));
            }
            return Ok(cart);
        }

        [HttpPost("{userId}/items")]
        public async Task<IActionResult> AddOrUpdateItem([FromBody] AddCartItemRequest request, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct) ?? new Cart(userId);
            cart.AddOrUpdateItem(request.ProductId, request.Quantity, request.UnitPrice);

            await _cartRepository.SaveCartAsync(cart, ct);
            return Ok(cart);
        }

        [HttpDelete("{userId}/items/{productId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is not null)
            {
                cart.RemoveItem(productId);
                await _cartRepository.SaveCartAsync(cart, ct);
            }
            return Ok(cart);
        }
    }

    public record AddCartItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);
}
