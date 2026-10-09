using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Common.Interfaces
{
    public record RazorpayOrderRequest(decimal Amount, string Currency, string ReceiptId);
    public record RazorpayOrderResponse(string PaymentOrderId, decimal Amount, string Currency, string Status);
    public record RazorpayVerificationRequest(string RazorpayPaymentId, string RazorpayOrderId, string RazorpaySignature);

    public interface IPaymentGateway
    {
        Task<RazorpayOrderResponse> CreatePaymentOrderAsync(RazorpayOrderRequest request, CancellationToken ct = default);
        bool VerifyPaymentSignature(RazorpayVerificationRequest request);
    }
}
