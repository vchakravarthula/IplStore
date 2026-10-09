using IplStore.Application.Products.Commands.DeductStock;
using IplStore.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly GetProductsQueryHandler _getProductsHandler;
        

        public ProductsController(
            GetProductsQueryHandler getProductsHandler)
        {
            _getProductsHandler = getProductsHandler;
        }

        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetProducts([FromQuery] Guid? franchiseId, [FromQuery] Guid? categoryId, CancellationToken ct)
        {
            var products = await _getProductsHandler.HandleAsync(franchiseId, categoryId, ct);
            return Ok(products);
        }

        //[HttpPost("{id:guid}/deduct-stock")]
        //public async Task<IActionResult> DeductStock(Guid id, [FromBody] DeductStockRequest request, CancellationToken ct)
        //{
        //    var command = new DeductStockCommand(id, request.Quantity);
        //    var result = await _deductStockHandler.HandleAsync(command, ct);

        //    if (!result) return NotFound(new { Message = $"Product with ID {id} not found." });

        //    return Ok(new { Message = "Stock updated successfully." });
        //}
    }

    //public record DeductStockRequest(int Quantity);
}
