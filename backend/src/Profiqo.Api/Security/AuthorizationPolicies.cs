using Microsoft.AspNetCore.Authorization;

using Profiqo.Domain.Users;

namespace Profiqo.Api.Security;

public static class AuthorizationPolicies
{
    public const string OwnerOnly = "OwnerOnly";
    public const string ReportAccess = "ReportAccess";
    public const string IntegrationAccess = "IntegrationAccess";

    public static IServiceCollection AddProfiqoAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(OwnerOnly, p => p.RequireRole(nameof(UserRole.Owner)));

            options.AddPolicy(ReportAccess, p =>
                p.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Admin), nameof(UserRole.Integration), nameof(UserRole.Reporting)));

            options.AddPolicy(IntegrationAccess, p =>
                p.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Admin), nameof(UserRole.Integration)));
        });

        return services;
    }
}