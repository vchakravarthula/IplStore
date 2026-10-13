using System.Text;
using IplStore.Api.Health;
using IplStore.Api.Middleware;
using IplStore.Application;
using IplStore.Infrastructure;
using IplStore.Infrastructure.Data;
using IplStore.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ---------- Observability: traces, metrics and logs over OTLP (Aspire Dashboard locally, Azure Monitor in prod) ----------
if (builder.Configuration.GetValue("Telemetry:Enabled", true))
{
    const string serviceName = "IplStore.Api";
    var otlp = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName))
        .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter(o => o.Endpoint = otlp))
        .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = otlp));

    builder.Logging.AddOpenTelemetry(o =>
    {
        o.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName));   // logs carry service.name too
        o.IncludeFormattedMessage = true;
        o.IncludeScopes = true;
        o.AddOtlpExporter(x => x.Endpoint = otlp);
    });
}

// ---------- Application services ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Catalog responses are cached for 30s per distinct query string. (Stock shown may be up to 30s stale;
// checkout always enforces real stock atomically.)
builder.Services.AddOutputCache(o =>
    o.AddPolicy("Catalog", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByQuery("*")));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader().AllowAnyMethod()));

// ---------- AuthN / AuthZ ----------
// One source of truth: the validated JwtSettings that AuthController also signs with. No hardcoded fallbacks.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwt) =>
    {
        options.MapInboundClaims = false;   // keep "sub" and "role" as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    // Secure by default: any endpoint without explicit metadata requires a signed-in user ([AllowAnonymous] opts out).
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

// ---------- Health ----------
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sql", tags: new[] { "ready" });
if (!string.Equals(builder.Configuration["Cart:Store"], "InMemory", StringComparison.OrdinalIgnoreCase))
    healthChecks.AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

var app = builder.Build();

if (builder.Configuration.GetValue("Database:Initialize", false))
    await DatabaseInitializer.InitializeAsync(app.Services, seed: builder.Configuration.GetValue("Database:Seed", false));

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();   // process is up
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous(); // dependencies are up

app.Run();

// Makes the entry point visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }
