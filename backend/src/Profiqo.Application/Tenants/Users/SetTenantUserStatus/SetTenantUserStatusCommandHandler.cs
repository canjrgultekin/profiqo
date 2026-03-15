using MediatR;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Users;

namespace Profiqo.Application.Tenants.Users.SetTenantUserStatus;

internal sealed class SetTenantUserStatusCommandHandler : IRequestHandler<SetTenantUserStatusCommand, bool>
{
    private readonly ITenantContext _tenant;
    private readonly ITenantUserRepository _repo;

    public SetTenantUserStatusCommandHandler(ITenantContext tenant, ITenantUserRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    public async Task<bool> Handle(SetTenantUserStatusCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new InvalidOperationException("Tenant context missing.");

        var user = await _repo.GetByTenantAndIdAsync(tenantId.Value, request.UserId, ct);
        if (user is null) return false;

        if (user.Roles.Contains(UserRole.Owner) && !request.IsActive)
            throw new InvalidOperationException("Owner cannot be disabled.");

        if (request.IsActive) user.Activate();
        else user.Disable();

        await _repo.SaveChangesAsync(ct);
        return true;
    }
}