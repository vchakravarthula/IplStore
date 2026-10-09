using IplStore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Products.Commands.DeductStock
{
    public record DeductStockCommand(Guid ProductId, int Quantity);
    public class DeductStockCommandHandler
    {
        private readonly IApplicationDbContext _context;

        public DeductStockCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HandleAsync(DeductStockCommand command, CancellationToken ct = default)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (true)
            {
                try
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == command.ProductId, ct);

                    if (product is null) return false;

                    product.DeductStock(command.Quantity);

                    await _context.SaveChangesAsync(ct);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries) throw;

                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * retryCount, ct); // Exponential backoff
                }
            }
        }
    }
}
