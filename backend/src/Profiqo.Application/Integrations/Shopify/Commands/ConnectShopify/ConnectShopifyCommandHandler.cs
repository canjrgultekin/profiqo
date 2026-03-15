// Path: backend/src/Profiqo.Application/Integrations/Shopify/Commands/ConnectShopify/ConnectShopifyCommandHandler.cs
using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Shopify;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Shopify.Commands.ConnectShopify;

internal sealed class ConnectShopifyCommandHandler : IRequestHandler<ConnectShopifyCommand, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IShopifyTokenService _tokenService;
    private readonly IShopifyGraphqlClient _client;

    public ConnectShopifyCommandHandler(
        ITenantContext tenant, IProviderConnectionRepository connections,
        ISecretProtector secrets, IShopifyTokenService tokenService,
        IShopifyGraphqlClient client)
    {
        _tenant = tenant; _connections = connections; _secrets = secrets;
        _tokenService = tokenService; _client = client;
    }

    public async Task<Guid> Handle(ConnectShopifyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var shopName = NormalizeShopName(request.ShopName);
        var clientId = (request.ClientId ?? "").Trim();
        var clientSecret = (request.ClientSecret ?? "").Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Shopify" : request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(shopName))
            throw new ArgumentException("ShopName required. Example: mystore or mystore.myshopify.com", nameof(request.ShopName));
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("ClientId required. Get it from Shopify Dev Dashboard > Your App > Settings.", nameof(request.ClientId));
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("ClientSecret required. Get it from Shopify Dev Dashboard > Your App > Settings.", nameof(request.ClientSecret));

        // 1) client_credentials grant ile token al (bağlantı doğrulama)
        var tokenResult = await _tokenService.AcquireTokenAsync(shopName, clientId, clientSecret, ct);

        // 2) Token'ı doğrula: shop query
        const string testQuery = "{ shop { name myshopifyDomain } }";
        using var testDoc = await _client.QueryAsync(shopName, tokenResult.AccessToken, testQuery, null, ct);

        var root = testDoc.RootElement;
        if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
        {
            var msg = errs[0].TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            throw new InvalidOperationException($"Shopify connection test failed: {msg}");
        }

        // 3) Credentials'ı encrypted sakla
        //    AccessToken  → encrypted { ClientId, ClientSecret }  (kalıcı app credentials)
        //    RefreshToken → encrypted shpat_xxx                   (24 saatte yenilenen token)
        //    AccessTokenExpiresAtUtc → shpat token'ın expire zamanı
        var credsJson = JsonSerializer.Serialize(new ShopifyCreds(clientId, clientSecret));
        var encCreds = _secrets.Protect(credsJson);
        var encToken = _secrets.Protect(tokenResult.AccessToken);

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Shopify, ct);

        if (existing is null)
        {
            var created = ProviderConnection.Create(
                tenantId: tenantId.Value,
                providerType: ProviderType.Shopify,
                displayName: displayName,
                externalAccountId: shopName,
                accessToken: encCreds,
                refreshToken: encToken,
                accessTokenExpiresAtUtc: tokenResult.ExpiresAtUtc,
                nowUtc: DateTimeOffset.UtcNow);

            await _connections.AddAsync(created, ct);
            return created.Id.Value;
        }

        existing.UpdateProfile(displayName, shopName, DateTimeOffset.UtcNow);
        existing.RotateTokens(encCreds, encToken, tokenResult.ExpiresAtUtc, DateTimeOffset.UtcNow);

        return existing.Id.Value;
    }

    internal sealed record ShopifyCreds(string ClientId, string ClientSecret);

    private static string NormalizeShopName(string? shopName)
    {
        var s = (shopName ?? "").Trim().ToLowerInvariant();
        if (s.StartsWith("https://")) s = s["https://".Length..];
        if (s.StartsWith("http://")) s = s["http://".Length..];
        s = s.TrimEnd('/');
        if (s.Contains(".myshopify.com")) s = s.Split('.')[0];
        var slash = s.IndexOf('/');
        if (slash > 0) s = s[..slash];
        return s;
    }
}