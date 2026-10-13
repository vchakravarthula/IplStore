namespace IplStore.Application.Common.Exceptions;

/// <summary>The payment provider failed. Maps to HTTP 502; the message may contain provider detail, so it is logged, never returned.</summary>
public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? inner = null) : base(message, inner) { }
}
