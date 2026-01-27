using FluentValidation;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Profiqo.Application.Common.Behaviors;

namespace Profiqo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProfiqoApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        services.AddScoped<Profiqo.Application.Integrations.Ikas.IIkasSyncProcessor, Profiqo.Application.Integrations.Ikas.IkasSyncProcessor>();
        services.AddScoped<Profiqo.Application.Customers.IdentityResolution.IIdentityResolutionService, Profiqo.Application.Customers.IdentityResolution.IdentityResolutionService>();

        return services;
    }
}