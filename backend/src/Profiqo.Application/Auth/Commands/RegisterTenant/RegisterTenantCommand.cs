using Profiqo.Application.Auth.DTOs;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Auth.Commands.RegisterTenant;

public sealed record RegisterTenantCommand(
    string TenantName,
    string TenantSlug,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerDisplayName
) : ICommand<LoginResultDto>;