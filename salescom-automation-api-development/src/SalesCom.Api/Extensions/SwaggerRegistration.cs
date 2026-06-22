namespace SalesCom.Api.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

/// <summary>
/// Swagger / OpenAPI registration. Enabled only in Development so the spec and UI never ship to
/// production — point a public SDK at the deployed spec by re-enabling per-environment if needed.
/// Configures JWT bearer so the "Authorize" button in Swagger UI lets you paste a token once and
/// have it applied to every "Try it out" request.
/// </summary>
internal static class SwaggerRegistration
{
    public static IServiceCollection AddSalesComSwagger(this IServiceCollection services) =>
        services.AddEndpointsApiExplorer().AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SalesCom API",
                Version = "v1",
                Description = "Sales commission microservice. Authenticate with a JWT issued by the central auth service.",
            });

            const string scheme = "Bearer";
            options.AddSecurityDefinition(scheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the JWT here. Do NOT prefix with 'Bearer ' — Swagger adds it.",
            });

            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(scheme, hostDocument: null, externalResource: null)] = new List<string>(),
            });

            options.SupportNonNullableReferenceTypes();
            options.CustomSchemaIds(t => t.FullName?.Replace('+', '.'));
        });

    public static WebApplication UseSalesComSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(opt =>
        {
            opt.SwaggerEndpoint("/swagger/v1/swagger.json", "SalesCom API v1");
            opt.DocumentTitle = "SalesCom API — Swagger";
            opt.RoutePrefix = "swagger";
            opt.DisplayRequestDuration();
            opt.EnableTryItOutByDefault();
            opt.EnablePersistAuthorization();
        });

        return app;
    }
}
