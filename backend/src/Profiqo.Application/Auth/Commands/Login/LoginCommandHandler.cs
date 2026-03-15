using MediatR;

using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Application.Auth.DTOs;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Users;

namespace Profiqo.Application.Auth.Commands.Login;

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _tenants = tenants;
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var slug = request.TenantSlug.Trim().ToLowerInvariant();

        var tenant = await _tenants.GetBySlugAsync(slug, cancellationToken);
        if (tenant is null)
            throw new UnauthorizedException("Invalid credentials.");

        var email = new EmailAddress(request.Email);
        var user = await _users.GetByEmailAsync(tenant.Id, email, cancellationToken);
        if (user is null)
            throw new UnauthorizedException("Invalid credentials.");

        if (user.Status != UserStatus.Active)
            throw new ForbiddenException("User is disabled.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        var now = DateTimeOffset.UtcNow;
        var token = _tokenService.IssueAccessToken(user, now);

        return new LoginResultDto(
            new AuthTokensDto(token.AccessToken, token.ExpiresAtUtc),
            new UserDto(
                UserId: user.Id.Value,
                TenantId: user.TenantId.Value,
                Email: user.Email.Value,
                DisplayName: user.DisplayName,
                Status: (short)user.Status,
                Roles: user.Roles.Select(r => (short)r).ToArray()
            )
        );
    }
}
