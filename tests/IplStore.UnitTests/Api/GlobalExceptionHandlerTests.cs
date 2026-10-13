using System.Text.Json;
using IplStore.Api.Middleware;
using IplStore.Application.Common.Exceptions;
using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.UnitTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    public static IEnumerable<object[]> Cases() => new[]
    {
        new object[] { new NotFoundException("Product", 1), 404 },
        new object[] { new InsufficientStockException("p", 5, 1), 409 },
        new object[] { new ConflictException("nope"), 409 },
        new object[] { new ValidationException("bad"), 400 },
        new object[] { new DbUpdateConcurrencyException("race"), 409 },
        new object[] { new DependencyUnavailableException("Cart store"), 503 },
        new object[] { new PaymentGatewayException("Razorpay returned HTTP 500: {secret detail}"), 502 },
        new object[] { new UnauthorizedAccessException(), 401 },
        new object[] { new OperationCanceledException(), 499 },
        new object[] { new InvalidOperationException("Sequence contains no elements"), 500 },
        new object[] { new ArgumentException("framework detail"), 500 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Exceptions_map_to_the_right_status(Exception exception, int expectedStatus)
        => Assert.Equal(expectedStatus, GlobalExceptionHandler.Map(exception).Status);

    [Fact]
    public async Task Unexpected_exceptions_never_leak_their_message_to_the_client()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("connection string Server=prod;Password=hunter2"), CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.False(body.Contains("hunter2"));
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task Payment_provider_detail_is_logged_but_not_returned()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, new PaymentGatewayException("Razorpay returned HTTP 400: {account 12345 suspended}"), CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(502, context.Response.StatusCode);
        Assert.False(body.Contains("12345"));
    }

    [Fact]
    public async Task Service_unavailable_tells_the_client_when_to_retry()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, new DependencyUnavailableException("Cart store"), CancellationToken.None);

        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Validation_errors_are_returned_in_the_errors_extension()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var errors = new Dictionary<string, string[]> { ["quantity"] = new[] { "must be positive" } };

        await handler.TryHandleAsync(context, new ValidationException(errors), CancellationToken.None);

        context.Response.Body.Position = 0;
        using var json = JsonDocument.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.Equal("must be positive", json.RootElement.GetProperty("errors").GetProperty("quantity")[0].GetString());
    }
}
