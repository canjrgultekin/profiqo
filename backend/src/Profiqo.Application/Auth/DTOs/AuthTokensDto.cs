namespace Profiqo.Application.Auth.DTOs;

public sealed record AuthTokensDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc);