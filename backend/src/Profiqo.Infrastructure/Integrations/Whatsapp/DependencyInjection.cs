using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Profiqo.Application.Abstractions.Integrations.Whatsapp;

namespace Profiqo.Infrastructure.Integrations.Whatsapp;

public static class DependencyInjection
{
    public static IServiceCollection AddWhatsappIntegration(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<WhatsappIntegrationOptions>(cfg.GetSection("Profiqo:Integrations:Whatsapp"));

        services.AddHttpClient<IWhatsappCloudValidator, WhatsappCloudValidator>();

        return services;
    }
}