using IplStore.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IplStore.IntegrationTests.Support;

/// <summary>
/// Boots the REAL application (Program.cs, controllers, auth, exception handler, EF model) against a throw-away
/// SQLite file, an in-memory cart and a fake payment provider. No Docker needed.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "integration-tests-signing-key-0123456789-abcdef";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"iplstore-it-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting (not ConfigureAppConfiguration): Program.cs reads these while it is still building the host.
        var settings = new Dictionary<string, string>
        {
            ["Persistence:Provider"] = "Sqlite",
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
            ["Cart:Store"] = "InMemory",
            ["Workers:Enabled"] = "false",
            ["Telemetry:Enabled"] = "false",
            ["Database:Initialize"] = "true",
            ["Database:Seed"] = "true",
            ["Jwt:Secret"] = TestSecret,
            ["Jwt:Issuer"] = "it-issuer",
            ["Jwt:Audience"] = "it-audience",
            ["Razorpay:KeyId"] = "rzp_test_integration",
            ["Razorpay:KeySecret"] = FakePaymentGateway.Secret,
            ["RabbitMQ:Host"] = "unused",
            ["RabbitMQ:Username"] = "unused",
            ["RabbitMQ:Password"] = "unused"
        };
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort cleanup of the temp database */ }
    }
}
