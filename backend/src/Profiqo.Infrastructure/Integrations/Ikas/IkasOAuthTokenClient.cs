using System.Net.Http.Headers;
using System.Text.Json;

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

    public async Task<IkasAccessTokenResponse> GetAccessTokenAsync2(string storeName, string clientId,
        string clientSecret, CancellationToken ct)
    {
      var  text =
        "{\"access_token\":\"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjI1OWM0M2U1LTJjZWEtNDQ3Yi05NWIyLWIyYWRhNWI3YmM4ZSIsImVtYWlsIjoiY2Fua2d0a2luQGdtYWlsLmNvbSIsImZpcnN0TmFtZSI6ImNhbiIsImxhc3ROYW1lIjoia8O8w6fDvGtnw7xsdGVraW4iLCJtZXJjaGFudElkIjoiNzNjNDI0NTUtYjY5ZC00ZDk3LTlmOWMtZjg1N2NkYTllMzVjIiwic3RvcmVOYW1lIjoicHJvZmlxbyIsImltYWdlSWQiOm51bGwsInR5cGUiOjEsImZlYXR1cmVzIjpbXSwibGFuZ3VhZ2UiOiJ0ciIsImxpbWl0cyI6eyIxIjoxMDAsIjIiOjEsIjMiOjEsIjQiOjEsIjUiOjIsIjYiOjEsIjciOjEsIjgiOjIsIjE1IjoxLCIxNyI6MSwiMTgiOjEsIjIzIjoxLCIyNCI6MSwiMjgiOjEsIjMzIjoxLCIzNCI6MSwiMzYiOjEsIjQwIjoxMCwiNDEiOjEsIjQzIjoxLCI0NCI6MSwiNDUiOjF9LCJzbEZlYXR1cmVzIjp7fSwibWZhIjowLCJpYXQiOjE3NzAwNTg2ODYsImV4cCI6MTc3MDE0NTA4NiwiYXVkIjoicHJvZmlxby5teWlrYXMuY29tIiwiaXNzIjoicHJvZmlxby5teWlrYXMuY29tIiwic3ViIjoiY2Fua2d0a2luQGdtYWlsLmNvbSJ9.-DUyHfHkJSOGQqinSzDI-EgSXWpqXF9Q9QjiPx9tXuI\",\"token_type\":\"Bearer\",\"expires_in\":1770145086}";

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
}
