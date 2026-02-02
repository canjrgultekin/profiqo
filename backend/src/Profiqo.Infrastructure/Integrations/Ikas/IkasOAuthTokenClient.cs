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
    private const string BaseUrl = "https://profiqo.myikas.com/api/admin/graphql?op=refreshToken";
    private const string CurrentToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjYwNzU2ZmU1LTZjZTEtNGQ5Ni1hYjkyLTk4NWIyMzU1YTEzYiIsImVtYWlsIjoiY2FuLmt1Y3VrZ3VsdGVraW5AZ21haWwuY29tIiwiZmlyc3ROYW1lIjoiY2FuIiwibGFzdE5hbWUiOiJrdWN1a2d1bHRla2luIiwibWVyY2hhbnRJZCI6IjczYzQyNDU1LWI2OWQtNGQ5Ny05ZjljLWY4NTdjZGE5ZTM1YyIsInN0b3JlTmFtZSI6InByb2ZpcW8iLCJpbWFnZUlkIjpudWxsLCJ0eXBlIjoyLCJmZWF0dXJlcyI6WzIsMyw0LDUsNTAwLDUwMSwxMSwxMiwxOCwxLDMwMSwzMDAsNyw4LDksMTAsMTMsMTUsMTcsMTRdLCJsYW5ndWFnZSI6InRyIiwibGltaXRzIjp7IjEiOjEwMCwiMiI6MSwiMyI6MSwiNCI6MSwiNSI6MiwiNiI6MSwiNyI6MSwiOCI6MiwiMTUiOjEsIjE3IjoxLCIxOCI6MSwiMjMiOjEsIjI0IjoxLCIyOCI6MSwiMzMiOjEsIjM0IjoxLCIzNiI6MSwiNDAiOjEwLCI0MSI6MSwiNDMiOjEsIjQ0IjoxLCI0NSI6MX0sInNsRmVhdHVyZXMiOnsiODBhYjUyMWQtZTdkZi00YWYxLTgwZDQtYjM1ZjBkYzY5ZTMyIjpbMjAxLDIwMCw0LDUsNTAwLDUwMSwxNiwyMDJdfSwibWZhIjowLCJpYXQiOjE3NzAwNjI0MDAsImV4cCI6MTc3MDE0ODgwMCwiYXVkIjoicHJvZmlxby5teWlrYXMuY29tIiwiaXNzIjoicHJvZmlxby5teWlrYXMuY29tIiwic3ViIjoiY2FuLmt1Y3VrZ3VsdGVraW5AZ21haWwuY29tIn0.sUh3XcH9KcEhdfLbYqEtNIJVROd-qje8xZvvKK6cl48";
    private const string RefreshTokenQuery = @"
        mutation refreshToken ($token: String!) {
            refreshToken (token: $token) {
                token 
                tokenExpiry 
            }
        }";


    public IkasOAuthTokenClient(HttpClient http)
    {
        _http = http;
    }
    public async Task<TokenResult?> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var request = new GraphQLRequest
        {
            Query = RefreshTokenQuery,
            Variables = new RefreshTokenVariables { Token = CurrentToken }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(cancellationToken);

            if (result?.Errors is { Count: > 0 })
            {
                return null;
            }


            return result?.Data?.RefreshToken;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    public async Task<IkasAccessTokenResponse> GetAccessTokenAsync2(string storeName, string clientId,
        string clientSecret, CancellationToken ct)
    {
        
        var newtokenData = await RefreshTokenAsync();
        var newToken = newtokenData.Token;
        var newTokenExp = newtokenData.TokenExpiry;
        var newTokenType = "Bearer";

        var tokenResponse = new
        {
            access_token = newToken,
            token_type = newTokenType,
            expires_in = newTokenExp
        };

        var text = JsonSerializer.Serialize(tokenResponse);
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
