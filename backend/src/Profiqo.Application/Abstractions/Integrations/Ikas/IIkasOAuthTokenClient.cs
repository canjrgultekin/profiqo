using System.Text.Json.Serialization;

namespace Profiqo.Application.Abstractions.Integrations.Ikas;

public sealed record IkasAccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public interface IIkasOAuthTokenClient
{
    Task<IkasAccessTokenResponse> GetAccessTokenAsync(string storeName, string clientId, string clientSecret, CancellationToken ct);
}