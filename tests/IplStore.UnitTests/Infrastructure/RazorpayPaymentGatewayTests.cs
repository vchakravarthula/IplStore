using System.Net;
using System.Security.Cryptography;
using System.Text;
using IplStore.Application.Common.Exceptions;
using IplStore.Application.Common.Interfaces;
using IplStore.Infrastructure.Options;
using IplStore.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace IplStore.UnitTests.Infrastructure;

public sealed class RazorpayPaymentGatewayTests
{
    private const string Secret = "test_secret_value";

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "{\"id\":\"order_ABC123\",\"status\":\"created\"}";
        public bool Throw { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Throw) throw new HttpRequestException("network down");
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }

    private static RazorpayPaymentGateway NewGateway(StubHandler handler) =>
        new(new HttpClient(handler), Options.Create(new RazorpaySettings { KeyId = "rzp_test_key", KeySecret = Secret }));

    private static string Sign(string orderId, string paymentId) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{orderId}|{paymentId}")))
            .ToLowerInvariant();

    [Fact]
    public async Task CreateOrder_sends_paise_as_an_integer_basic_auth_and_returns_the_gateway_order_id()
    {
        var handler = new StubHandler();

        var result = await NewGateway(handler).CreateOrderAsync(new PaymentOrderRequest(1999.5m, "INR", "IPL-ABC"));

        Assert.Equal("order_ABC123", result.GatewayOrderId);
        Assert.Equal("https://api.razorpay.com/v1/orders", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Contains("\"amount\":199950", handler.LastBody);   // 1999.50 INR = 199950 paise
        Assert.Contains("\"receipt\":\"IPL-ABC\"", handler.LastBody);
    }

    [Fact]
    public async Task Fractional_paise_are_rounded_not_truncated()
    {
        var handler = new StubHandler();
        await NewGateway(handler).CreateOrderAsync(new PaymentOrderRequest(0.295m, "INR", "r"));
        Assert.Contains("\"amount\":30", handler.LastBody);   // 29.5 paise -> 30 (the old (int) cast gave 29)
    }

    [Fact]
    public async Task Provider_errors_become_PaymentGatewayException()
    {
        var handler = new StubHandler { Status = HttpStatusCode.BadRequest, ResponseBody = "{\"error\":{\"description\":\"bad\"}}" };
        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            NewGateway(handler).CreateOrderAsync(new PaymentOrderRequest(10m, "INR", "r")));
    }

    [Fact]
    public async Task Network_failures_become_PaymentGatewayException()
    {
        var handler = new StubHandler { Throw = true };
        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            NewGateway(handler).CreateOrderAsync(new PaymentOrderRequest(10m, "INR", "r")));
    }

    [Fact]
    public async Task A_response_without_an_order_id_is_an_error()
    {
        var handler = new StubHandler { ResponseBody = "{\"status\":\"created\"}" };
        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            NewGateway(handler).CreateOrderAsync(new PaymentOrderRequest(10m, "INR", "r")));
    }

    [Fact]
    public void A_correct_signature_verifies()
    {
        var gateway = NewGateway(new StubHandler());
        Assert.True(gateway.VerifySignature(new PaymentVerificationRequest("order_1", "pay_1", Sign("order_1", "pay_1"))));
    }

    [Fact]
    public void Signature_casing_does_not_matter()
    {
        var gateway = NewGateway(new StubHandler());
        Assert.True(gateway.VerifySignature(new PaymentVerificationRequest("order_1", "pay_1", Sign("order_1", "pay_1").ToUpperInvariant())));
    }

    [Theory]
    [InlineData("order_1", "pay_2")]    // tampered payment id
    [InlineData("order_2", "pay_1")]    // tampered order id
    public void A_signature_for_different_ids_is_rejected(string orderId, string paymentId)
    {
        var gateway = NewGateway(new StubHandler());
        Assert.False(gateway.VerifySignature(new PaymentVerificationRequest(orderId, paymentId, Sign("order_1", "pay_1"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzz")]
    public void Garbage_signatures_are_rejected_without_throwing(string signature)
    {
        var gateway = NewGateway(new StubHandler());
        Assert.False(gateway.VerifySignature(new PaymentVerificationRequest("order_1", "pay_1", signature)));
    }
}
