using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

public sealed record RestockRequest(int Quantity);

/// <summary>Policy-based authorization demo: only tokens carrying the Admin role get past the "AdminOnly" policy.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public AdminController(IInventoryService inventory) => _inventory = inventory;

    [HttpPost("products/{productId:guid}/restock")]
    public async Task<IActionResult> Restock(Guid productId, [FromBody] RestockRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0) throw new ValidationException("Quantity must be greater than zero.");

        await _inventory.ReleaseAsync(new[] { new StockLine(productId, request.Quantity) }, ct);
        return NoContent();
    }
}
