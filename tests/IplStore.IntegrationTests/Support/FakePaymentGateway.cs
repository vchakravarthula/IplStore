using System.Security.Cryptography;
using System.Text;
using IplStore.Application.Common.Interfaces;

namespace IplStore.IntegrationTests.Support;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string ValidSignature = "valid-signature";
    public const string Secret = "integration-secret";

    public Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentOrderResult($"order_{Guid.NewGuid():N}"[..20], request.Amount, request.Currency, "created"));

    // Accepts the literal test signature, or a genuine HMAC computed with the configured secret (what the dev endpoint produces).
    public bool VerifySignature(PaymentVerificationRequest request) =>
        request.Signature == ValidSignature || request.Signature == Sign(request.GatewayOrderId, request.GatewayPaymentId);

    private static string Sign(string orderId, string paymentId) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{orderId}|{paymentId}")))
            .ToLowerInvariant();
}
