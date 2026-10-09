using IplStore.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Common.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task SaveCartAsync(Cart cart, CancellationToken ct = default);
        Task DeleteCartAsync(string userId, CancellationToken ct = default);
    }
}
