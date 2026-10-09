using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Data.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IplStore.Infrastructure.Services
{
    public class RazorpayPaymentGateway : IPaymentGateway
    {
        private readonly RazorpaySettings _settings;

        public RazorpayPaymentGateway(IOptions<RazorpaySettings> settings)
        {
            _settings = settings.Value;

            if (string.IsNullOrWhiteSpace(_settings.KeyId) || string.IsNullOrWhiteSpace(_settings.KeySecret))
            {
                throw new InvalidOperationException("Razorpay credentials are not properly configured.");
            }
        }

        public void Initialize()
        {
            // Use injected, validated configuration values without fallbacks
            var client = new Razorpay.Api.RazorpayClient(_settings.KeyId, _settings.KeySecret);
        }

        public Task<RazorpayOrderResponse> CreatePaymentOrderAsync(RazorpayOrderRequest request, CancellationToken ct = default)
        {
            // 1. Fixed: Use validated _settings properties instead of unassigned _key and _secret
            var client = new RazorpayClient(_settings.KeyId, _settings.KeySecret);

            var options = new Dictionary<string, object>
        {
            { "amount", (int)(request.Amount * 100) }, // Razorpay expects amount in paise
            { "currency", request.Currency },
            { "receipt", request.ReceiptId },
            { "payment_capture", 1 }
        };

            // 2. Wrap synchronous SDK call to prevent blocking thread pool worker threads
            return Task.Run(() =>
            {
                Order order = client.Order.Create(options);
                var orderId = order["id"].ToString()!;
                var status = order["status"].ToString()!;

                return new RazorpayOrderResponse(orderId, request.Amount, request.Currency, status);
            }, ct);
        }

        public bool VerifyPaymentSignature(RazorpayVerificationRequest request)
        {
            var payload = $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}";

            // 3. Fixed: Use _settings.KeySecret for HMACSHA256 signature verification
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.KeySecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var generatedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            return generatedSignature.Equals(request.RazorpaySignature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
