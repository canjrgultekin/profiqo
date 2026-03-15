using MediatR;

namespace Profiqo.Application.Tenants.Users.ListTenantUsers;

public sealed record ListTenantUsersQuery() : IRequest<IReadOnlyList<TenantUserItemDto>>;