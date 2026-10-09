using IplStore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Common.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task UpdateAsync(Product product, CancellationToken ct = default);
    }
}
