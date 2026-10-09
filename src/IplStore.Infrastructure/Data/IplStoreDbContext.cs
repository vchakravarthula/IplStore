using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Infrastructure.Data
{
    public class IplStoreDbContext : DbContext, IApplicationDbContext
    {
        public IplStoreDbContext(DbContextOptions<IplStoreDbContext> options) : base(options) { }

        public DbSet<Franchise> Franchises => Set<Franchise>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(IplStoreDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
