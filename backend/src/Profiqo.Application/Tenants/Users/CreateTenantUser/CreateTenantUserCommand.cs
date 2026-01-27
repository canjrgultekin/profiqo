using MediatR;

namespace Profiqo.Application.Tenants.Users.CreateTenantUser;

public sealed record CreateTenantUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string Role
) : IRequest<Guid>;