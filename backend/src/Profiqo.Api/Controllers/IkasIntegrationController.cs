// Path: backend/src/Profiqo.Api/Controllers/IkasIntegrationController.cs
using System.Text.Json;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Profiqo.Api.Security;
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Integrations.Ikas;
using Profiqo.Application.Abstractions.Persistence;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Integrations.Ikas.Commands.ConnectIkas;
using Profiqo.Application.Integrations.Ikas.Commands.StartIkasSync;
using Profiqo.Application.Integrations.Ikas.Commands.TestIkas;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Integrations;

namespace Profiqo.Api.Controllers;

[ApiController]
[Route("api/integrations/ikas")]
[Authorize(Policy = AuthorizationPolicies.IntegrationAccess)]
public sealed class IkasIntegrationController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IIntegrationJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IProviderConnectionRepository _connections;
    private readonly ISecretProtector _secrets;
    private readonly IIkasGraphqlClient _ikas;
    private readonly IIkasOAuthTokenClient _oauth;

    public IkasIntegrationController(
        ISender sender,
        IIntegrationJobRepository jobs,
        ITenantContext tenant,
        IProviderConnectionRepository connections,
        ISecretProtector secrets,
        IIkasGraphqlClient ikas,
        IIkasOAuthTokenClient oauth)
    {
        _sender = sender;
        _jobs = jobs;
        _tenant = tenant;
        _connections = connections;
        _secrets = secrets;
        _ikas = ikas;
        _oauth = oauth;
    }

    // ── Mevcut endpoint'ler ─────────────────────────────────────

    [HttpGet("connection")]
    public async Task<IActionResult> GetConnection(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct);
        if (conn is null)
            return Ok(new { hasConnection = false });

        return Ok(new
        {
            hasConnection = true,
            connectionId = conn.Id.Value,
            providerType = conn.ProviderType.ToString(),
            status = conn.Status.ToString(),
            displayName = conn.DisplayName,
            externalAccountId = conn.ExternalAccountId,
            accessTokenExpiresAtUtc = conn.AccessTokenExpiresAtUtc
        });
    }

    public sealed record ConnectRequest(string StoreLabel, string StoreName, string? ClientId, string? ClientSecret);

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        var id = await _sender.Send(
            new ConnectIkasCommand(req.StoreLabel, req.StoreName, req.ClientId ?? string.Empty, req.ClientSecret ?? string.Empty),
            ct);

        return Ok(new { connectionId = id });
    }

    public sealed record TestRequest(Guid ConnectionId);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestRequest req, CancellationToken ct)
    {
        var meId = await _sender.Send(new TestIkasCommand(req.ConnectionId), ct);
        return Ok(new { ok = true, meId });
    }

    public sealed record StartRequest(Guid ConnectionId, string Scope, int? PageSize, int? MaxPages);

    [HttpPost("sync/start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        var scope = string.IsNullOrWhiteSpace(req.Scope) ? "both" : req.Scope;

        var result = await _sender.Send(
            new StartIkasSyncCommand(req.ConnectionId, scope, req.PageSize, req.MaxPages),
            ct);

        return Accepted(result);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob([FromRoute] Guid jobId, CancellationToken ct)
    {
        var j = await _jobs.GetAsync(jobId, ct);
        if (j is null) return NotFound(new { message = "Job not found." });
        return Ok(j);
    }

    [HttpGet("jobs/batch/{batchId:guid}")]
    public async Task<IActionResult> GetBatch([FromRoute] Guid batchId, CancellationToken ct)
    {
        var list = await _jobs.ListByBatchAsync(batchId, ct);
        return Ok(new { batchId, jobs = list });
    }

    // ── Storefront yönetimi — Script entegrasyonu ───────────────

    /// <summary>Merchant'ın ikas storefront listesini döner.</summary>
    [HttpGet("storefronts")]
    public async Task<IActionResult> ListStorefronts(CancellationToken ct)
    {
        var (storeName, accessToken) = await ResolveIkasCredentialsAsync(ct);

        using var doc = await _ikas.ListStorefrontsAsync(storeName, accessToken, ct);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("listStorefront", out var list))
        {
            return Ok(new { storefronts = Array.Empty<object>() });
        }

        var storefronts = new List<object>();
        foreach (var sf in list.EnumerateArray())
        {
            var deleted = sf.TryGetProperty("deleted", out var d) && d.GetBoolean();
            if (deleted) continue;

            var routings = new List<object>();
            if (sf.TryGetProperty("routings", out var routingsArr))
            {
                foreach (var r in routingsArr.EnumerateArray())
                {
                    var rDeleted = r.TryGetProperty("deleted", out var rd) && rd.GetBoolean();
                    if (rDeleted) continue;

                    routings.Add(new
                    {
                        id = r.TryGetProperty("id", out var rid) ? rid.GetString() : null,
                        domain = r.TryGetProperty("domain", out var rdom) ? rdom.GetString() : null,
                        locale = r.TryGetProperty("locale", out var rloc) ? rloc.GetString() : null,
                        path = r.TryGetProperty("path", out var rpath) ? rpath.GetString() : null,
                        currencyCode = r.TryGetProperty("currencyCode", out var rcc) ? rcc.GetString() : null,
                        countryCodes = r.TryGetProperty("countryCodes", out var rctry) && rctry.ValueKind == JsonValueKind.Array
                            ? rctry.EnumerateArray().Select(x => x.GetString()).ToList()
                            : new List<string?>()
                    });
                }
            }

            storefronts.Add(new
            {
                id = sf.TryGetProperty("id", out var sid) ? sid.GetString() : null,
                name = sf.TryGetProperty("name", out var sn) ? sn.GetString() : null,
                salesChannelId = sf.TryGetProperty("salesChannelId", out var sc) ? sc.GetString() : null,
                routings
            });
        }

        return Ok(new { storefronts });
    }

    public sealed record InstallScriptRequest(string StorefrontId, string ScriptContent, string ScriptName, bool IsHighPriority = true);

    /// <summary>Storefront'a Profiqo pixel script'ini yükler.</summary>
    [HttpPost("install-script")]
    public async Task<IActionResult> InstallScript([FromBody] InstallScriptRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.StorefrontId))
            return BadRequest(new { message = "StorefrontId zorunlu." });

        if (string.IsNullOrWhiteSpace(req.ScriptContent))
            return BadRequest(new { message = "ScriptContent zorunlu." });

        var scriptName = string.IsNullOrWhiteSpace(req.ScriptName) ? "ProfiqoPixel" : req.ScriptName.Trim();

        var (storeName, accessToken) = await ResolveIkasCredentialsAsync(ct);

        using var doc = await _ikas.SaveStorefrontJSScriptAsync(
            storeName, accessToken,
            req.StorefrontId.Trim(),
            req.ScriptContent.Trim(),
            scriptName,
            req.IsHighPriority,
            ct);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("saveStorefrontJSScript", out var result))
        {
            // GraphQL errors varsa yakala
            var errMsg = "ikas API'den geçersiz yanıt.";
            if (doc.RootElement.TryGetProperty("errors", out var errs) &&
                errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
            {
                var firstErr = errs[0];
                errMsg = firstErr.TryGetProperty("message", out var em) ? em.GetString() ?? errMsg : errMsg;
            }

            return StatusCode(502, new { message = errMsg, raw = doc.RootElement.ToString() });
        }

        return Ok(new
        {
            success = true,
            scriptId = result.TryGetProperty("id", out var id) ? id.GetString() : null,
            scriptName,
            storefrontId = req.StorefrontId.Trim()
        });
    }

    // ── Token çözümleme helper ──────────────────────────────────

    private sealed record IkasPrivateAppCreds(string StoreName, string ClientId, string ClientSecret);

    /// <summary>Mevcut ikas bağlantısından storeName ve accessToken çözer. TestIkasCommandHandler ile aynı pattern.</summary>
    private async Task<(string StoreName, string AccessToken)> ResolveIkasCredentialsAsync(CancellationToken ct)
    {
        var tenantId = _tenant.CurrentTenantId;
        if (tenantId is null)
            throw new InvalidOperationException("X-Tenant-Id header is required." );


        var conn = await _connections.GetByProviderAsync(tenantId.Value, ProviderType.Ikas, ct)
            ?? throw new InvalidOperationException("ikas bağlantısı bulunamadı. Önce API bağlantısı kurun.");

        var storeName = (conn.ExternalAccountId ?? string.Empty).Trim();
        var raw = _secrets.Unprotect(conn.AccessToken);

        // Private app credentials JSON olarak saklanmışsa → OAuth ile token al
        if (TryParseCreds(raw, out var creds))
        {
            storeName = string.IsNullOrWhiteSpace(creds.StoreName) ? storeName : creds.StoreName.Trim();
            var token = await _oauth.GetAccessTokenAsync(storeName, creds.ClientId, creds.ClientSecret, ct);
            return (storeName, token.AccessToken);
        }

        // Doğrudan bearer token
        return (storeName, raw);
    }

    private static bool TryParseCreds(string raw, out IkasPrivateAppCreds creds)
    {
        creds = default!;
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!raw.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
            var c = JsonSerializer.Deserialize<IkasPrivateAppCreds>(raw);
            if (c is null) return false;
            if (string.IsNullOrWhiteSpace(c.StoreName) || string.IsNullOrWhiteSpace(c.ClientId) || string.IsNullOrWhiteSpace(c.ClientSecret))
                return false;
            creds = c;
            return true;
        }
        catch { return false; }
    }
}