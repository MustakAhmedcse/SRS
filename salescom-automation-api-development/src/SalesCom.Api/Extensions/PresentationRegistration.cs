namespace SalesCom.Api.Extensions;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SalesCom.Api.GlobalHandlers;
using SalesCom.Application.Common;

/// <summary>
/// Aggregates every presentation-layer service registration so <c>Program.cs</c> stays a
/// three-liner. Owned by the API project — wires controllers, JSON, the global exception handler,
/// the unified-envelope model-binding response, Swagger (Development), and health checks.
/// </summary>
public static class PresentationRegistration
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        // Model-binding failures (malformed JSON, missing required fields detected by the binder)
        // bypass our Result pipeline — wrap them in the unified envelope here so clients still
        // get one consistent shape. Multiple validation messages are joined into the single
        // Message field with "; " separators.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .SelectMany(kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage))
                    .ToList();

                var message = errors.Count > 0 ? string.Join("; ", errors) : "One or more validation errors occurred.";
                return new BadRequestObjectResult(ApiResponse.Fail(message, "Validation.ModelBinding"));
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        // ASP.NET Core 10's ExceptionHandlerMiddleware requires an IProblemDetailsService at
        // construction even when an IExceptionHandler is registered — it's the framework's
        // fallback path. Our handler always returns true and writes the unified ApiResponse,
        // so this registration is only there to satisfy the middleware's DI requirement.
        services.AddProblemDetails();

        services.AddSalesComSwagger();
        services.AddHealthChecks();

        return services;
    }
}
