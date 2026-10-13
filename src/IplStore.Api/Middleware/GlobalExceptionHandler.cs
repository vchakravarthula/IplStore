using System.Diagnostics;
using IplStore.Application.Common.Exceptions;
using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Api.Middleware;

/// <summary>
/// The single place exceptions become HTTP responses (RFC 7807 ProblemDetails with a traceId).
/// Business errors are 4xx and logged quietly; only genuine faults are 5xx, logged with the stack trace, and never leak internals.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var (status, title, detail, level, errors) = Map(exception);

        _logger.Log(level, level >= LogLevel.Error ? exception : null,
            "Request failed with {StatusCode}: {ErrorMessage} (trace {TraceId})", status, exception.Message, traceId);

        if (context.Response.HasStarted) return true;   // too late to change the response

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path };
        problem.Extensions["traceId"] = traceId;
        if (errors is not null) problem.Extensions["errors"] = errors;

        if (status == StatusCodes.Status503ServiceUnavailable)
            context.Response.Headers.RetryAfter = "5";

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }

    public static (int Status, string Title, string Detail, LogLevel Level, IDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            NotFoundException e => (404, "Resource not found", e.Message, LogLevel.Information, null),
            InsufficientStockException e => (409, "Insufficient stock", e.Message, LogLevel.Information, null),
            ConflictException e => (409, "Conflict", e.Message, LogLevel.Information, null),
            ValidationException e => (400, "Validation failed", e.Message, LogLevel.Information, e.Errors.Count > 0 ? e.Errors : null),
            DomainException e => (400, "Business rule violation", e.Message, LogLevel.Warning, null),
            DbUpdateConcurrencyException => (409, "Concurrency conflict", "The resource was changed by another request. Reload and retry.", LogLevel.Warning, null),
            DependencyUnavailableException e => (503, "Service temporarily unavailable", $"{e.Dependency} is unavailable. Please retry shortly.", LogLevel.Error, null),
            PaymentGatewayException => (502, "Payment provider error", "The payment provider could not process the request. Please retry.", LogLevel.Error, null),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication is required.", LogLevel.Information, null),
            OperationCanceledException => (499, "Request cancelled", "The client cancelled the request.", LogLevel.Information, null),
            _ => (500, "Internal server error", "An unexpected error occurred.", LogLevel.Error, null)
        };
}
