using IplStore.Application.Common.Exceptions;
using IplStore.Application.Common.Interfaces;

namespace IplStore.UnitTests.Support;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool FailOnCreate { get; set; }
    public bool SignaturesAreValid { get; set; } = true;
    public int CreateCalls { get; private set; }

    public Task<PaymentOrderResult> CreateOrderAsync(PaymentOrderRequest request, CancellationToken ct = default)
    {
        CreateCalls++;
        if (FailOnCreate) throw new PaymentGatewayException("simulated provider outage");
        return Task.FromResult(new PaymentOrderResult($"order_{Guid.NewGuid():N}"[..20], request.Amount, request.Currency, "created"));
    }

    public bool VerifySignature(PaymentVerificationRequest request) => SignaturesAreValid;
}

public sealed class FakeMessagePublisher : IMessagePublisher
{
    public List<(string Queue, string Payload)> Published { get; } = new();
    public bool Fail { get; set; }

    public Task PublishAsync(string queueName, string payload, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("broker down");
        Published.Add((queueName, payload));
        return Task.CompletedTask;
    }
}
