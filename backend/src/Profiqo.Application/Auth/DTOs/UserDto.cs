namespace Profiqo.Application.Auth.DTOs;

public sealed record UserDto(
    Guid UserId,
    Guid TenantId,
    string Email,
    string DisplayName,
    short Status,
    short[] Roles);