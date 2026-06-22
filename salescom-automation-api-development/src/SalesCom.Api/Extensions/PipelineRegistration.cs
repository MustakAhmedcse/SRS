namespace SalesCom.Api.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using SalesCom.Api.Middleware;
using SalesCom.Application.Interfaces;

/// <summary>
/// Wires the HTTP request pipeline. Middleware order is significant: correlation ID → exception →
/// request log → Swagger → HTTPS → auth → controllers/health. Each subsystem stays in its own
/// extension so re-ordering is a one-liner.
/// </summary>
public static class PipelineRegistration
{
    public static WebApplication UseSalesComPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // First: stamp a correlation ID so every downstream log line (including the request-log
        // and exception-handler entries) carries it.
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseSerilogRequestLogging(opt =>
        {
            opt.GetLevel = (ctx, _, ex) => ex is not null || ctx.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : LogEventLevel.Information;

            // Attach the authenticated caller to each request-completion log so activity is
            // traceable per user across the fleet (alongside the ambient CorrelationId).
            opt.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                var currentUser = httpContext.RequestServices.GetService<ICurrentUser>();
                if (currentUser is { IsAuthenticated: true })
                {
                    diagnosticContext.Set("UserId", currentUser.UserId);
                    diagnosticContext.Set("UserName", currentUser.UserName);
                }
            };
        });

        app.UseSalesComSwagger();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
