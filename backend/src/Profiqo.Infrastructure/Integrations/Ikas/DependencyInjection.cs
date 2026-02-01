using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Profiqo.Application.Abstractions.Integrations.Ikas;

namespace Profiqo.Infrastructure.Integrations.Ikas;

public static class DependencyInjection
{
    public static IServiceCollection AddIkasIntegration(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<IkasOptions>(cfg.GetSection("Profiqo:Integrations:Ikas"));
        services.AddHttpClient<IIkasGraphqlClient, IkasGraphqlClient>();
        services.AddHttpClient<IIkasOAuthTokenClient, IkasOAuthTokenClient>();

        services.AddScoped<IIkasSyncStore, IkasSyncStore>();
        return services;
    }
}