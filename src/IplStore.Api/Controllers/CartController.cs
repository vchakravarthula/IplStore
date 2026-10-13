using IplStore.Api.Extensions;
using IplStore.Application.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);

public sealed record SetCartItemQuantityRequest(int Quantity);

/// <summary>Every route is "my cart": the user id comes from the token, so there is no id to tamper with.</summary>
[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController : ControllerBase
{
    private readonly CartService _cart;

    public CartController(CartService cart) => _cart = cart;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _cart.GetAsync(User.GetUserId(), ct));

    // The request has no price: the server always prices from the catalog.
    [HttpPost("items")]
    public async Task<IActionResult> Add([FromBody] AddCartItemRequest request, CancellationToken ct) =>
        Ok(await _cart.AddAsync(User.GetUserId(), request.ProductId, request.Quantity, ct));

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> SetQuantity(Guid productId, [FromBody] SetCartItemQuantityRequest request, CancellationToken ct) =>
        Ok(await _cart.SetQuantityAsync(User.GetUserId(), productId, request.Quantity, ct));

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct) =>
        Ok(await _cart.RemoveAsync(User.GetUserId(), productId, ct));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _cart.ClearAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
