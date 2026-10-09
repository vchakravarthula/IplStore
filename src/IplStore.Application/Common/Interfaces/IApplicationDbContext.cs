using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Franchise> Franchises { get; }
        DbSet<Category> Categories { get; }
        DbSet<Product> Products { get; }
        DbSet<Cart> Carts { get; }
        DbSet<CartItem> CartItems { get; }
        DbSet<Order> Orders { get; }
        DbSet<OrderItem> OrderItems { get; }
        DbSet<OutboxMessage> OutboxMessages { get; }

        DatabaseFacade Database { get; } 

        ChangeTracker ChangeTracker { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
