using System.Text.Json;

using MediatR;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Shopify;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Common.Exceptions;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Shopify.Commands.TestShopify;

internal sealed class TestShopifyCommandHandler : IRequestHandler<TestShopifyCommand, bool>
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IShopifyTokenService _tokenService;
    private readonly IShopifyGraphqlClient _client;

    public TestShopifyCommandHandler(ITenantContext tenant, IProviderConnectionRepository connections,
        ISecretProtector secrets, IShopifyTokenService tokenService, IShopifyGraphqlClient client)
    {
        _tenant = tenant; _connections = connections; _secrets = secrets;
        _tokenService = tokenService; _client = client;
    }

    public async Task<bool> Handle(TestShopifyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null) throw new UnauthorizedException("Tenant context missing.");

        var conn = await _connections.GetByIdAsync(new ProviderConnectionId(request.ConnectionId), ct)
            ?? await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Shopify, ct);

        if (conn is null || conn.TenantId != tenantId.Value || conn.ProviderType != ProviderType.Shopify)
            throw new NotFoundException($"Shopify connection not found. connectionId={request.ConnectionId}");

        var shopName = conn.ExternalAccountId ?? throw new InvalidOperationException("ShopName missing.");
        var token = await ResolveTokenAsync(conn, shopName, ct);

        const string testQuery = "{ shop { name myshopifyDomain } }";
        using var doc = await _client.QueryAsync(shopName, token, testQuery, null, ct);

        if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
            throw new InvalidOperationException("Shopify test query returned errors.");

        return true;
    }

    private async Task<string> ResolveTokenAsync(ProviderConnection conn, string shopName, CancellationToken ct)
    {
        // RefreshToken alanında cached shpat token var mı ve hala geçerli mi?
        if (!string.IsNullOrWhiteSpace(conn.RefreshToken.ToString())
            && conn.AccessTokenExpiresAtUtc.HasValue
            && conn.AccessTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _secrets.Unprotect(conn.RefreshToken);
        }

        // Expired veya yok: AccessToken alanından creds'i oku, yeni token al
        var credsJson = _secrets.Unprotect(conn.AccessToken);
        var creds = JsonSerializer.Deserialize<ShopifyCreds>(credsJson)
            ?? throw new InvalidOperationException("Shopify credentials invalid in DB.");

        var result = await _tokenService.AcquireTokenAsync(shopName, creds.ClientId, creds.ClientSecret, ct);

        // Token'ı DB'ye yaz (RefreshToken alanına)
        var encToken = _secrets.Protect(result.AccessToken);
        conn.RotateTokens(conn.AccessToken, encToken, result.ExpiresAtUtc, DateTimeOffset.UtcNow);

        return result.AccessToken;
    }

    private sealed record ShopifyCreds(string ClientId, string ClientSecret);
}