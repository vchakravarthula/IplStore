using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IplStore.Application.Common.Exceptions;
using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure.Services;

/// <summary>
/// Razorpay over plain HttpClient (typed client): fully async, traced by the OpenTelemetry HTTP instrumentation,
/// and wrapped by the resilience pipeline (timeout, retry, circuit breaker) registered in DependencyInjection.
/// </summary>
public sealed class RazorpayPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly RazorpaySettings _settings;

    public RazorpayPaymentGateway(HttpClient http, IOptions<RazorpaySettings> settings)
    {
        _http = http;
        _settings = settings.Value;

        _http.BaseAddress ??= new Uri("https://api.razorpay.com/");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.KeyId}:{_settings.KeySecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken ct = default)
    {
        // Razorpay wants the smallest currency unit (paise) as an integer. Round, never truncate.
        var minorUnits = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "v1/orders",
                new { amount = minorUnits, currency = request.Currency, receipt = request.Receipt },
                ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new PaymentGatewayException($"Razorpay returned HTTP {(int)response.StatusCode}: {body}");

            using var json = JsonDocument.Parse(body);
            var id = json.RootElement.GetProperty("id").GetString()
                     ?? throw new PaymentGatewayException("Razorpay response had no order id.");
            var status = json.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;

            return new PaymentOrderResult(id, request.Amount, request.Currency, status ?? "created");
        }
        catch (Exception ex) when (ex is not PaymentGatewayException && !ct.IsCancellationRequested)
        {
            // Network errors, timeouts, open circuit breaker, malformed JSON: one exception type for callers.
            throw new PaymentGatewayException("Razorpay order creation failed.", ex);
        }
    }

    public bool VerifySignature(PaymentVerificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GatewayOrderId) ||
            string.IsNullOrWhiteSpace(request.GatewayPaymentId) ||
            string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var payload = Encoding.UTF8.GetBytes($"{request.GatewayOrderId}|{request.GatewayPaymentId}");
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_settings.KeySecret), payload))
            .ToLowerInvariant();

        // Constant-time comparison: do not leak how many leading characters matched.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(request.Signature.Trim().ToLowerInvariant()));
    }
}
