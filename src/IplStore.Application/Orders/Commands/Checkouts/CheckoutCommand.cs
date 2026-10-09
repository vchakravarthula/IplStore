using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using IplStore.Application.Common.Interfaces;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IplStore.Domain.Exceptions;
using MediatR;

namespace IplStore.Application.Orders.Commands.Checkouts
{
    public record CheckoutCommand(string UserId) : IRequest<CheckoutResponse>;

    public record CheckoutResponse(Guid OrderId, string ShortOrderNumber, decimal TotalAmount, string RazorpayPaymentOrderId);

    public class CheckoutCommandHandler
    {
        private readonly IApplicationDbContext _dbContext;
        private readonly ICartRepository _cartRepository;
        private readonly IPaymentGateway _paymentGateway;
        private const string BaseCurrency = "INR";

        public CheckoutCommandHandler(
            IApplicationDbContext dbContext,
            ICartRepository cartRepository,
            IPaymentGateway paymentGateway)
        {
            _dbContext = dbContext;
            _cartRepository = cartRepository;
            _paymentGateway = paymentGateway;
        }

        public async Task<CheckoutResponse> HandleAsync(CheckoutCommand command, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(command.UserId, ct);
            if (cart is null || !cart.Items.Any())
            {
                throw new InvalidOperationException("Cannot checkout an empty or non-existent cart.");
            }

            var productIds = cart.Items.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            // Map cart items into Order domain tuples (Snapshots price at purchase)
            var orderItems = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>();

            foreach (var item in cart.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    throw new InvalidOperationException($"Product ID {item.ProductId} not found.");
                }

                product.DeductStock(item.Quantity);
                orderItems.Add((product.Id, product.Name, item.Quantity, product.Price));
            }

            var order = new Order(command.UserId, orderItems);

            // Initiate Razorpay Gateway Order
            var razorpayOrder = await _paymentGateway.CreatePaymentOrderAsync(
                new RazorpayOrderRequest(order.TotalAmount, BaseCurrency, order.ShortOrderNumber), ct);

            // 5. Save order and commit changes inside an explicit transaction boundary
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            });

            // Clear cart from Redis upon successful checkout initiation
            await _cartRepository.DeleteCartAsync(command.UserId, ct);

            return new CheckoutResponse(order.Id, order.ShortOrderNumber, order.TotalAmount, razorpayOrder.PaymentOrderId);
        }
    }
}
