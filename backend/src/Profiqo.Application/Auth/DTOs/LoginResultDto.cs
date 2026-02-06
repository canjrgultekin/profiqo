namespace Profiqo.Application.Auth.DTOs;

public sealed record LoginResultDto(
    AuthTokensDto Tokens,
    UserDto User);