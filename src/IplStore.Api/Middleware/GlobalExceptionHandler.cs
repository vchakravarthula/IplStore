using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace IplStore.Api.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            _logger.LogError(exception, "An unhandled exception occurred. TraceId: {TraceId}", traceId);

            var (statusCode, title, detail, errors, logLevel) = exception switch
            {
                // --- Domain & Business Rule Exceptions (4xx) ---
                NotFoundException ex => (
                    StatusCodes.Status404NotFound,
                    "Resource Not Found",
                    ex.Message,
                    null,
                    LogLevel.Information),

                InsufficientStockException ex => (
                    StatusCodes.Status409Conflict,
                    "Insufficient Stock",
                    ex.Message,
                    null,
                    LogLevel.Warning),

                ValidationException ex => (
                    StatusCodes.Status400BadRequest,
                    "Validation Error",
                    ex.Message,
                    ex.Errors.Count > 0 ? ex.Errors : null,
                    LogLevel.Warning),

                DomainException ex => (
                    StatusCodes.Status400BadRequest,
                    "Business Rule Violation",
                    ex.Message,
                    null,
                    LogLevel.Warning),

                // --- Database Concurrency Violations ---
                DbUpdateConcurrencyException => (
                    StatusCodes.Status409Conflict,
                    "Concurrency Conflict",
                    "The record you attempted to edit was modified by another user. Please reload and try again.",
                    null,
                    LogLevel.Warning),

                // --- Request Aborted / Canceled ---
                OperationCanceledException or TaskCanceledException => (
                    499, // Client Closed Request
                    "Request Canceled",
                    "The operation was canceled by the client.",
                    null,
                    LogLevel.Information),

                // --- Fallback Framework / Unhandled Exceptions (5xx) ---
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred. Please try again later.",
                    null,
                    LogLevel.Error)
            };

            // Log using the contextually appropriate log level (4xx as Info/Warn, 5xx as Error)
            if (logLevel == LogLevel.Error)
            {
                _logger.LogError(exception, "Unhandled exception occurred while processing request. TraceId: {TraceId}", traceId);
            }
            else
            {
                _logger.Log(logLevel, "Handled expected exception ({StatusCode}): {Message}. TraceId: {TraceId}", statusCode, exception.Message, traceId);
            }

            httpContext.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] = traceId;

            if (errors != null)
            {
                problemDetails.Extensions["errors"] = errors;
            }

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
