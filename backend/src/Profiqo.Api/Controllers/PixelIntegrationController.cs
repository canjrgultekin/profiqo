using System.Security.Cryptography;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

/// <summary>
/// Pixel (Storefront Events) entegrasyon yönetimi — Admin panel için.
/// provider_connections tablosunda ProviderType.Pixel kaydı oluşturur/günceller.
/// external_account_id = publicApiKey (pfq_pub_XXXX)
/// </summary>
[ApiController]
[Route("api/integrations/pixel")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class PixelIntegrationController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly IUnitOfWork _uow;
    private readonly ISecretProtector _secrets;

    public PixelIntegrationController(
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        IUnitOfWork uow,
        ISecretProtector secrets)
    {
        _tenant = tenant;
        _connections = connections;
        _uow = uow;
        _secrets = secrets;
    }

    /// <summary>
    /// Mevcut Pixel bağlantı bilgisini getir.
    /// GET /api/integrations/pixel/connection
    /// </summary>
    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Pixel, ct);
        if (conn is null)
            return Ok(new { hasConnection = false });

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            providerType = conn.ProviderType.ToString(),
            status = conn.Status.ToString(),
            displayName = conn.DisplayName,
            publicApiKey = conn.ExternalAccountId,
            createdAtUtc = conn.CreatedAtUtc,
            updatedAtUtc = conn.UpdatedAtUtc
        });
    }

    public sealed record ConnectPixelRequest(string DisplayName);

    /// <summary>
    /// Pixel bağlantısı oluştur veya güncelle.
    /// POST /api/integrations/pixel/connect
    /// publicApiKey otomatik üretilir (pfq_pub_XXXX).
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectPixelRequest req, CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });

        var existing = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Pixel, ct);

        if (existing is not null)
        {
            // Güncelle — displayName değiştirilir, apiKey aynı kalır
            existing.UpdateProfile(req.DisplayName.Trim(), existing.ExternalAccountId, DateTimeOffset.UtcNow);
            await _uow.SaveChangesAsync(ct);

            return Ok(new
            {
                connectionId = existing.Id.Value,
                publicApiKey = existing.ExternalAccountId,
                updated = true
            });
        }

        // Yeni oluştur
        var publicApiKey = GeneratePublicApiKey();

        // Pixel'de gerçek bir token gerekmiyor — placeholder encrypt
        var placeholderEncrypted = _secrets.Protect("pixel_no_token");

        var conn = ProviderConnection.Create(
            tenantId: tenantId.Value,
            providerType: ProviderType.Pixel,
            displayName: req.DisplayName.Trim(),
            externalAccountId: publicApiKey,
            accessToken: placeholderEncrypted,
            refreshToken: null,
            accessTokenExpiresAtUtc: null,
            nowUtc: DateTimeOffset.UtcNow);

        await _connections.AddAsync(conn, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(new
        {
            connectionId = conn.Id.Value,
            publicApiKey,
            updated = false
        });
    }

    /// <summary>
    /// publicApiKey yeniden oluştur (rotate).
    /// POST /api/integrations/pixel/rotate-key
    /// </summary>
    [HttpPost("rotate-key")]
    public async Task<IActionResult> RotateKey(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Pixel, ct);
        if (conn is null)
            return NotFound(new { message = "Pixel connection not found." });

        var newKey = GeneratePublicApiKey();
        conn.UpdateProfile(conn.DisplayName, newKey, DateTimeOffset.UtcNow);
        await _uow.SaveChangesAsync(ct);

        return Ok(new
        {
            connectionId = conn.Id.Value,
            publicApiKey = newKey,
            rotated = true
        });
    }

    /// <summary>
    /// Script tag'i oluştur — admin panelden kopyalanacak.
    /// GET /api/integrations/pixel/script-tag
    /// </summary>
    [HttpGet("script-tag")]
    public async Task<IActionResult> GetScriptTag(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Pixel, ct);
        if (conn is null)
            return NotFound(new { message = "Pixel connection not found." });

        var scriptBaseUrl = "https://profiqocdn.z1.web.core.windows.net/ikas/v2";
        var scriptFileName = "profiqo-ikas-events.min.js";

        var tag = $"<script src=\"{scriptBaseUrl}/{scriptFileName}?apiKey={conn.ExternalAccountId}\" async></script>";

        return Ok(new
        {
            publicApiKey = conn.ExternalAccountId,
            scriptBaseUrl,
            scriptFileName,
            scriptTag = tag,
            endpoint = "https://api.profiqo.com"
        });
    }

    /// <summary>pfq_pub_ + 32 hex char = 40 char public key</summary>
    private static string GeneratePublicApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return "pfq_pub_" + Convert.ToHexStringLower(bytes);
    }
}
