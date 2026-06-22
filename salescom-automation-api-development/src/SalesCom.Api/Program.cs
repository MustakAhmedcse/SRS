using SalesCom.Api.Extensions;
using SalesCom.Application;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure;
using SalesCom.Infrastructure.Registrations;
using SalesCom.Infrastructure.Services;
using Serilog;

// Bootstrap logger captures failures during host construction before the real Serilog config runs.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.UseSalesComSerilog();

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplication()
        .AddPresentation();
    var app = builder.Build();
    app.UseSalesComPipeline();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "SalesCom host terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Required for Microsoft.AspNetCore.Mvc.Testing to discover the entry point.
namespace SalesCom.Api
{
    public partial class Program;
}
