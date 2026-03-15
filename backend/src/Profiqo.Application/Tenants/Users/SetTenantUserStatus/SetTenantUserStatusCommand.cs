using MediatR;

namespace Profiqo.Application.Tenants.Users.SetTenantUserStatus;

public sealed record SetTenantUserStatusCommand(Guid UserId, bool IsActive) : IRequest<bool>;