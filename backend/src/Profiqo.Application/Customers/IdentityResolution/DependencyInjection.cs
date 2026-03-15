using Microsoft.Extensions.DependencyInjection;

namespace Profiqo.Application.Customers.IdentityResolution;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityResolution(this IServiceCollection services)
    {
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        return services;
    }
}