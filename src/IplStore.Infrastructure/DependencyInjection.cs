using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.BackgroundJobs;
using IplStore.Infrastructure.Data;
using IplStore.Infrastructure.Data.Interceptors;
using IplStore.Infrastructure.Messaging;
using IplStore.Infrastructure.Options;
using IplStore.Infrastructure.Repositories;
using IplStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IplStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Secrets have NO fallbacks: missing configuration fails fast at startup instead of silently using a committed key.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RazorpaySettings>()
            .Bind(configuration.GetSection(RazorpaySettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        AddPersistence(services, configuration);
        AddCart(services, configuration);
        AddPayments(services);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set it with dotnet user-secrets or an environment variable.");
        var useSqlite = string.Equals(configuration["Persistence:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

        services.AddSingleton<ConvertDomainEventsToOutboxMessagesInterceptor>();
        services.AddDbContext<IplStoreDbContext>((sp, options) =>
        {
            if (useSqlite)
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());   // transient-fault retries

            options.AddInterceptors(sp.GetRequiredService<ConvertDomainEventsToOutboxMessagesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IplStoreDbContext>());
        services.AddScoped<IInventoryService, SqlInventoryService>();
    }

    private static void AddCart(IServiceCollection services, IConfiguration configuration)
    {
        if (string.Equals(configuration["Cart:Store"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICartRepository, InMemoryCartRepository>();
            return;
        }

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured (or set Cart:Store=InMemory for local runs).");

        var redisOptions = ConfigurationOptions.Parse(redisConnection);
        redisOptions.AbortOnConnectFail = false;   // start even if Redis is down; it reconnects in the background

        // Factory delegate: the connection is opened on first use, not at registration time.
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddSingleton<ICartRepository, RedisCartRepository>();
    }

    private static void AddPayments(IServiceCollection services)
    {
        // Standard resilience pipeline: total timeout, retry with jitter, circuit breaker, attempt timeout.
        services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>().AddStandardResilienceHandler();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();   // lazy: connects when first resolved
        services.AddScoped<OutboxProcessor>();

        // Workers are switched off for tests (Workers:Enabled=false).
        if (configuration.GetValue("Workers:Enabled", true))
        {
            services.AddHostedService<OutboxProcessorBackgroundService>();
            services.AddHostedService<ExpirePendingOrdersBackgroundService>();
        }
    }
}
