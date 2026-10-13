using IplStore.Application.Cart;
using IplStore.Application.Orders.Commands;
using IplStore.Application.Orders.Queries;
using IplStore.Application.Products.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace IplStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetProductsQueryHandler>();
        services.AddScoped<GetProductByIdQueryHandler>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutCommandHandler>();
        services.AddScoped<ConfirmPaymentCommandHandler>();
        services.AddScoped<ExpirePendingOrdersCommandHandler>();
        services.AddScoped<GetOrdersQueryHandler>();
        return services;
    }
}
