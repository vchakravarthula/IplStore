using IplStore.Api.Middleware;
using IplStore.Application.Common.Interfaces;
using IplStore.Application.Orders.Commands.Checkouts;
using IplStore.Application.Products.Commands.DeductStock;
using IplStore.Application.Products.Queries.GetProducts;
using IplStore.Infrastructure;
using IplStore.Infrastructure.BackgroundJobs;
using IplStore.Infrastructure.Messaging;
using IplStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure OpenTelemetry & Aspire Dashboard Exporter
const string serviceName = "IplStore.Api";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
});

// 2. Exception Handling & Controllers
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCaching();

// 3. Infrastructure & Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddScoped<IPaymentGateway, RazorpayPaymentGateway>();
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

// 4. CQRS Application Handlers Registration
builder.Services.AddScoped<GetProductsQueryHandler>();
builder.Services.AddScoped<DeductStockCommandHandler>();
builder.Services.AddScoped<CheckoutCommandHandler>();

// 5. Authentication & Authorization Setup
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "SuperSecretKeyForDevDemoPurposeOnly1234567890!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "IplStoreDev",
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "IplStoreDevAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 6. CORS Configuration for React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("AllowFrontend");
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
