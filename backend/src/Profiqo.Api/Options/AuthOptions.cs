namespace Profiqo.Api.Options;

public sealed class AuthOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string JwtSigningKey { get; init; } = string.Empty;
    public bool RegistrationEnabled { get; init; } = true;

}