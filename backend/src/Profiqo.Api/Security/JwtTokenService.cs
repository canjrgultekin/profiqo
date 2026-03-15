using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Profiqo.Api.Options;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Domain.Users;

namespace Profiqo.Api.Security;

internal sealed class JwtTokenService : ITokenService
{
    private readonly AuthOptions _opts;

    public JwtTokenService(IOptions<AuthOptions> options)
    {
        _opts = options.Value;
        if (string.IsNullOrWhiteSpace(_opts.JwtSigningKey) || _opts.JwtSigningKey.Length < 32)
            throw new InvalidOperationException("Profiqo:Auth:JwtSigningKey must be set (min 32 chars).");
    }

    public AccessTokenIssueResult IssueAccessToken(User user, DateTimeOffset nowUtc)
    {
        var expires = nowUtc.AddHours(8);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new("tenant_id", user.TenantId.Value.ToString()),
            new(ClaimTypes.Email, user.Email.Value),
            new("display_name", user.DisplayName)
        };

        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenIssueResult(jwt, expires);
    }
}