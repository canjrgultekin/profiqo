using MediatR;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Users;

using DomainUser = Profiqo.Domain.Users.User;

namespace Profiqo.Application.Tenants.Users.CreateTenantUser;

internal sealed class CreateTenantUserCommandHandler : IRequestHandler<CreateTenantUserCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly ITenantUserRepository _repo;
    private readonly IPasswordHasher _hasher;

    public CreateTenantUserCommandHandler(ITenantContext tenant, ITenantUserRepository repo, IPasswordHasher hasher)
    {
        _tenant = tenant;
        _repo = repo;
        _hasher = hasher;
    }

    public async Task<Guid> Handle(CreateTenantUserCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new InvalidOperationException("Tenant context missing.");

        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var displayName = (request.DisplayName ?? "").Trim();
        var password = request.Password ?? "";
        var roleRaw = (request.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Email required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) throw new InvalidOperationException("Password min 8 chars.");

        if (!Enum.TryParse<UserRole>(roleRaw, true, out var role)) throw new InvalidOperationException("Invalid role.");
        if (role == UserRole.Owner) throw new InvalidOperationException("Owner role cannot be assigned from UI.");

        var exists = await _repo.ExistsByTenantEmailAsync(tenantId.Value, email, ct);
        if (exists) throw new InvalidOperationException("Email already exists in this tenant.");

        var user = DomainUser.Create(
            UserId.New(),
            tenantId.Value,
            new EmailAddress(email),
            _hasher.Hash(password),
            string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            new[] { role });

        await _repo.AddAsync(user, ct);
        return user.Id.Value;
    }
}
