namespace SalesCom.Api.GlobalHandlers;

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesCom.Application.Common;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Translates uncaught exceptions into the unified <see cref="ApiResponse"/> envelope so clients
/// see one body shape across success, business errors, and server crashes. Domain failures should
/// normally surface through <c>Result</c>; this handler is the safety net for everything else.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The handler is a singleton, so the scoped unit of work is resolved from the request scope
        // rather than constructor-injected. Ensure any pending changes are discarded on unhandled exceptions.
        await httpContext.RequestServices.GetRequiredService<IUnitOfWork>().Rollback();

        // Name the exact failure up front (type + the method that threw) so the log line is precise
        // on its own; the attached exception still carries the full stack trace for Loki/Grafana.
        var faultingMethod = exception.TargetSite is { } site
            ? $"{site.DeclaringType?.FullName ?? "?"}.{site.Name}"
            : "unknown";

        logger.LogError(
            exception,
            "Unhandled {ExceptionType} in {FaultingMethod} handling {HttpMethod} {Path}: {ErrorMessage}",
            exception.GetType().FullName,
            faultingMethod,
            httpContext.Request.Method,
            httpContext.Request.Path,
            exception.Message);

        var (status, message, code) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, $"Validation failed: {ve.Message}", "Validation.Failed"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied.", "User.Forbidden"),
            OperationCanceledException => (499, "The request was cancelled.", "Request.Cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "Server.UnexpectedError"),
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(message, code), cancellationToken);
        return true;
    }
}
