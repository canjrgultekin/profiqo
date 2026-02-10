using System.Buffers.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Common.Exceptions;

using static System.Net.Mime.MediaTypeNames;

namespace Profiqo.Infrastructure.Integrations.Ikas;

internal sealed class IkasOAuthTokenClient : IIkasOAuthTokenClient
{
    private readonly HttpClient _http;



    public IkasOAuthTokenClient(HttpClient http)
    {
        _http = http;
    }
    public async Task<IkasAccessTokenResponse> GetAccessTokenAsync(string storeName, string clientId, string clientSecret, CancellationToken ct)
    {
        storeName = (storeName ?? string.Empty).Trim();
        clientId = (clientId ?? string.Empty).Trim();
        clientSecret = (clientSecret ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("storeName required.", nameof(storeName));
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("clientId required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("clientSecret required.", nameof(clientSecret));

        var url = $"https://{storeName}.myikas.com/api/admin/oauth/token";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var text = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new ExternalServiceAuthException("ikas", $"Ikas oauth token request failed: HTTP {(int)res.StatusCode}.");

        try
        {
            var dto = JsonSerializer.Deserialize<IkasAccessTokenResponse>(text);
            if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
                throw new InvalidOperationException("Ikas oauth token response missing access_token.");
            return dto;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Ikas oauth token returned invalid JSON: {text}");
        }
    }

    public sealed class GraphQLRequest
    {
        [JsonPropertyName("query")]
        public required string Query { get; init; }

        [JsonPropertyName("variables")]
        public required object Variables { get; init; }
    }

    public sealed class RefreshTokenVariables
    {
        [JsonPropertyName("token")]
        public required string Token { get; init; }
    }

    public sealed class RefreshTokenResponse
    {
        [JsonPropertyName("data")]
        public RefreshTokenData? Data { get; init; }

        [JsonPropertyName("errors")]
        public List<GraphQLError>? Errors { get; init; }
    }

    public sealed class RefreshTokenData
    {
        [JsonPropertyName("refreshToken")]
        public TokenResult? RefreshToken { get; init; }
    }
    public sealed class TokenResult
    {
        [JsonPropertyName("token")]
        public string? Token { get; init; }

        [JsonPropertyName("tokenExpiry")]
        public long? TokenExpiry { get; init; }
    }

    public sealed class GraphQLError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
