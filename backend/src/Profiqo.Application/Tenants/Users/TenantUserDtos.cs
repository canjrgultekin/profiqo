namespace Profiqo.Application.Tenants.Users;

public sealed record TenantUserItemDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc);