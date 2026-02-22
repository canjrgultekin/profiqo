using System.Reflection.Emit;

using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Profiqo.Application.Abstractions.Id;
using Profiqo.Application.Abstractions.Integrations.Trendyol;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Customers.Dedupe;
using Profiqo.Application.Integrations.Trendyol;
using Profiqo.Infrastructure.Integrations.Trendyol;
using Profiqo.Infrastructure.Persistence.Interceptors;
using Profiqo.Infrastructure.Persistence.Repositories;
using Profiqo.Infrastructure.Services;
using Profiqo.Infrastructure.StorefrontEvents;

namespace Profiqo.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddProfiqoPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddScoped<TenantEnforcementSaveChangesInterceptor>();

        services.AddDbContext<ProfiqoDbContext>((sp, options) =>
        {
            var connString = configuration.GetConnectionString("ProfiqoDb");
            if (string.IsNullOrWhiteSpace(connString))
                throw new InvalidOperationException("ConnectionStrings:ProfiqoDb is missing.");

            options.UseNpgsql(connString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }).UseSnakeCaseNamingConvention();

            options.AddInterceptors(
                sp.GetRequiredService<AuditingSaveChangesInterceptor>(),
                sp.GetRequiredService<TenantEnforcementSaveChangesInterceptor>());

            options.EnableDetailedErrors(false);
            options.EnableSensitiveDataLogging(false);
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProviderConnectionRepository, ProviderConnectionRepository>();
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.IIntegrationJobRepository, Profiqo.Infrastructure.Persistence.Repositories.IntegrationJobRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.Repositories.ICustomerRepository, Profiqo.Infrastructure.Persistence.Repositories.CustomerRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.IIntegrationCursorRepository, Profiqo.Infrastructure.Persistence.Repositories.IntegrationCursorRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.Repositories.ITenantUserRepository, Profiqo.Infrastructure.Persistence.Repositories.TenantUserRepository>();

        services.Configure<TrendyolOptions>(configuration.GetSection("Profiqo:Integrations:Trendyol"));

        services.AddHttpClient<ITrendyolClient, TrendyolClient>();
        services.AddScoped<ITrendyolSyncStore, TrendyolSyncStore>();
        services.AddScoped<ITrendyolSyncProcessor, TrendyolSyncProcessor>();
        // ✅ Dedupe analysis
        services.AddScoped<ICustomerDedupeAnalysisRepository, CustomerDedupeAnalysisRepository>();

        // ✅ NEW: suggestions repository
        services.AddScoped<ICustomerMergeSuggestionRepository, CustomerMergeSuggestionRepository>();
        // WhatsApp templates (DB table üzerinden SQL repo)
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.Whatsapp.IWhatsappTemplateRepository,
            Profiqo.Infrastructure.Persistence.Repositories.WhatsappTemplateRepository>();

        // WhatsApp automation
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.WhatsappAutomation.IWhatsappTemplateDraftRepository,
            Profiqo.Infrastructure.Persistence.Repositories.WhatsappTemplateDraftRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.WhatsappAutomation.IWhatsappRuleRepository,
            Profiqo.Infrastructure.Persistence.Repositories.WhatsappRuleRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.WhatsappAutomation.IWhatsappJobRepository,
            Profiqo.Infrastructure.Persistence.Repositories.WhatsappJobRepository>();
        services.AddScoped<Profiqo.Application.Abstractions.Persistence.WhatsappAutomation.IWhatsappDispatchRepository,
            Profiqo.Infrastructure.Persistence.Repositories.WhatsappDispatchRepository>();
       services.AddStorefrontEventProjection();

        return services;
    }
}
