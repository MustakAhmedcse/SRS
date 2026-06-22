namespace SalesCom.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Serilog.Context;

/// <summary>
/// Gives every request a correlation ID — reused from the inbound <c>X-Correlation-ID</c> header
/// when the caller supplies one, otherwise freshly generated. The ID is pushed into Serilog's
/// <see cref="LogContext"/> so <b>every log line written while handling the request carries it</b>,
/// set as <see cref="HttpContext.TraceIdentifier"/>, and echoed back in the response header so a
/// caller can quote it when reporting a problem.
/// <para>
/// Registered first in the pipeline (see <c>PipelineRegistration</c>) so request-logging and
/// exception-handler entries are tagged too.
/// </para>
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var inbound) && !string.IsNullOrWhiteSpace(inbound)
                ? inbound.ToString()
                : Guid.CreateVersion7().ToString("n");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers[HeaderName] = ctx.TraceIdentifier;
            return Task.CompletedTask;
        }, context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
