using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Data;
using IplStore.Infrastructure.Data.Configurations;
using IplStore.Infrastructure.Data.Interceptors;
using IplStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {

            // Bind and fail fast on startup if JwtSettings or RazorpaySettings are missing
            services.AddOptions<JwtSettings>()
                .Bind(configuration.GetSection(JwtSettings.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<RazorpaySettings>()
                .Bind(configuration.GetSection(RazorpaySettings.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=localhost,1433;Database=IplStoreDb;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;";

            services.AddSingleton<ConvertDomainEventsToOutboxMessagesInterceptor>();

            services.AddDbContext<IplStoreDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<ConvertDomainEventsToOutboxMessagesInterceptor>();
                options.UseSqlServer(connectionString)
                       .AddInterceptors(interceptor);
            });

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IplStoreDbContext>());

            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddScoped<ICartRepository, RedisCartRepository>();

            return services;
        }
    }
}
