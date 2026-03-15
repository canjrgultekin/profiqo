using MediatR;

using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Tenants.Users.CreateTenantUser;

public sealed record CreateTenantUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string Role
) : ICommand<Guid>;