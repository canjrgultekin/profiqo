using System.Security.Claims;

namespace Profiqo.Api.Security;

public sealed class CurrentUserContext
{
    public Guid? UserId { get; }
    public Guid? TenantId { get; }
    public string? Email { get; }

    public CurrentUserContext(IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;
        var user = http?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return;

        UserId = TryGuid(user, ClaimTypes.NameIdentifier) ?? TryGuid(user, "sub");
        TenantId = TryGuid(user, "tenant_id");
        Email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
    }

    private static Guid? TryGuid(ClaimsPrincipal user, string claimType)
    {
        var v = user.FindFirstValue(claimType);
        return Guid.TryParse(v, out var g) ? g : null;
    }
}