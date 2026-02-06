using MediatR;

using Profiqo.Application.Abstractions.Id;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Application.Auth.DTOs;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Tenants;
using Profiqo.Domain.Users;

namespace Profiqo.Application.Auth.Commands.RegisterTenant;

internal sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, LoginResultDto>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IIdGenerator _ids;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public RegisterTenantCommandHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IIdGenerator ids,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _tenants = tenants;
        _users = users;
        _ids = ids;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResultDto> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var slug = request.TenantSlug.Trim().ToLowerInvariant();

        var existing = await _tenants.GetBySlugAsync(slug, cancellationToken);
        if (existing is not null)
            throw new ConflictException("Tenant slug is already in use.");

        var tenant = Tenant.Create( request.TenantName.Trim(), slug,DateTimeOffset.UtcNow);

        var email = new EmailAddress(request.OwnerEmail);
        var passwordHash = _passwordHasher.Hash(request.OwnerPassword);

        var ownerUserId = new UserId(_ids.NewGuid());
        var owner = User.CreateOwner(ownerUserId, tenant.Id, email, passwordHash, request.OwnerDisplayName.Trim());

        await _tenants.AddAsync(tenant, cancellationToken);
        await _users.AddAsync(owner, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var token = _tokenService.IssueAccessToken(owner, now);

        return new LoginResultDto(
            new AuthTokensDto(token.AccessToken, token.ExpiresAtUtc),
            new UserDto(
                UserId: owner.Id.Value,
                TenantId: tenant.Id.Value,
                Email: owner.Email.Value,
                DisplayName: owner.DisplayName,
                Status: (short)owner.Status,
                Roles: owner.Roles.Select(r => (short)r).ToArray()
            )
        );
    }
}
