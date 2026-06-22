namespace SalesCom.Application;

using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalesCom.Application.Behaviours;
using SalesCom.Application.Interfaces;
using SalesCom.Application.Messaging;

/// <summary>
/// Composition root for the Application layer. Scans this assembly for command/query handlers
/// and validators and wires them through the decorator chain by hand — no MediatR, no Scrutor.
/// Decorator order (outermost → innermost): Logging → Validation → Handler.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        

        RegisterValidators(services, assembly);
        RegisterCommandHandlers(services, assembly);
        RegisterQueryHandlers(services, assembly);

        return services;
    }

    private static void RegisterValidators(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var validatorInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));

            foreach (var validatorInterface in validatorInterfaces)
            {
                services.AddTransient(validatorInterface, type);
            }
        }
    }

    private static void RegisterCommandHandlers(IServiceCollection services, Assembly assembly)
    {
        var openHandlerType = typeof(ICommandHandler<,>);
        var handlerImpls = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerType)
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlerImpls)
        {
            var commandType = service.GetGenericArguments()[0];
            var responseType = service.GetGenericArguments()[1];

            // Innermost: real handler registered against a "raw" keyed service we resolve inside the decorators chain.
            // We register the decorator stack as the public ICommandHandler<,> and let each decorator resolve its inner.
            services.TryAddTransient(implementation);

            var loggingDecorator = typeof(LoggingCommandHandlerDecorator<,>).MakeGenericType(commandType, responseType);
            var validationDecorator = typeof(ValidationCommandHandlerDecorator<,>).MakeGenericType(commandType, responseType);

            services.AddTransient(service, sp =>
            {
                var real = sp.GetRequiredService(implementation);
                var validation = ActivatorUtilities.CreateInstance(sp, validationDecorator, real);
                var logging = ActivatorUtilities.CreateInstance(sp, loggingDecorator, validation);
                return logging;
            });
        }
    }

    private static void RegisterQueryHandlers(IServiceCollection services, Assembly assembly)
    {
        var openHandlerType = typeof(IQueryHandler<,>);
        var handlerImpls = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerType)
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlerImpls)
        {
            var queryType = service.GetGenericArguments()[0];
            var responseType = service.GetGenericArguments()[1];

            services.TryAddTransient(implementation);

            var loggingDecorator = typeof(LoggingQueryHandlerDecorator<,>).MakeGenericType(queryType, responseType);

            services.AddTransient(service, sp =>
            {
                var real = sp.GetRequiredService(implementation);
                var logging = ActivatorUtilities.CreateInstance(sp, loggingDecorator, real);
                return logging;
            });
        }
    }
}
