// Path: backend/src/Profiqo.Infrastructure/StorefrontEvents/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

using Profiqo.Application.StorefrontEvents;

namespace Profiqo.Infrastructure.StorefrontEvents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStorefrontEventProjection(this IServiceCollection services)
    {
        services.AddScoped<IStorefrontCheckoutProjector, StorefrontCheckoutProjector>();
        return services;
    }
}