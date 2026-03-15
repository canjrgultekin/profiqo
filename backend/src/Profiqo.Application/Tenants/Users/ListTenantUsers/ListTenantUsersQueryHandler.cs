using MediatR;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;

namespace Profiqo.Application.Tenants.Users.ListTenantUsers;

internal sealed class ListTenantUsersQueryHandler : IRequestHandler<ListTenantUsersQuery, IReadOnlyList<TenantUserItemDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITenantUserRepository _repo;

    public ListTenantUsersQueryHandler(ITenantContext tenant, ITenantUserRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    public async Task<IReadOnlyList<TenantUserItemDto>> Handle(ListTenantUsersQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) return Array.Empty<TenantUserItemDto>();

        return await _repo.ListAsync(tenantId.Value, ct);
    }
}