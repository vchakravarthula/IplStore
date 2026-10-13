namespace IplStore.Application.Common.Interfaces;

// Provider-neutral contracts: swapping Razorpay for Stripe touches only the Infrastructure implementation.
public sealed record PaymentOrderRequest(decimal Amount, string Currency, string Receipt);

public sealed record PaymentOrderResult(string GatewayOrderId, decimal Amount, string Currency, string Status);

public sealed record PaymentVerificationRequest(string GatewayOrderId, string GatewayPaymentId, string Signature);

public interface IPaymentGateway
{
    /// <exception cref="Exceptions.PaymentGatewayException">The provider rejected the call or was unreachable.</exception>
    Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken ct = default);

    bool VerifySignature(PaymentVerificationRequest request);
}
