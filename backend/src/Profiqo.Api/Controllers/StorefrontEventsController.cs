using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Profiqo.Application.StorefrontEvents;

namespace Profiqo.Api.Controllers;

/// <summary>
/// Storefront event ingestion — ikas mağazadan gelen event'leri alır.
/// Bu endpoint'ler PUBLIC (JWT auth yok) — doğrulama apiKey ile yapılır.
/// </summary>
[ApiController]
[Route("api/v1/events")]
[AllowAnonymous]
public sealed class StorefrontEventsController : ControllerBase
{
    private readonly IPixelTenantResolver _tenantResolver;
    private readonly IStorefrontEventService _eventService;
    private readonly ILogger<StorefrontEventsController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StorefrontEventsController(
        IPixelTenantResolver tenantResolver,
        IStorefrontEventService eventService,
        ILogger<StorefrontEventsController> logger)
    {
        _tenantResolver = tenantResolver;
        _eventService = eventService;
        _logger = logger;
    }

    /// <summary>
    /// JS script başlangıçta çağırır — tenant config döner.
    /// GET /api/v1/events/storefront/config?apiKey=pfq_pub_XXX
    /// </summary>
    [HttpGet("storefront/config")]
    [DisableRateLimiting]
    public async Task<IActionResult> GetConfig(
        [FromQuery] string? apiKey, CancellationToken ct)
    {
        apiKey ??= Request.Headers["X-Profiqo-ApiKey"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest(new { error = "Missing apiKey" });

        var tenant = await _tenantResolver.ResolveAsync(apiKey, ct);
        if (tenant is null)
            return NotFound(new { error = "Invalid apiKey" });

        return Ok(new StorefrontConfigResponse
        {
            TenantId = tenant.TenantId.Value.ToString(),
            TenantName = tenant.TenantName,
            StoreDomain = tenant.StoreDomain,
            EnabledEvents = tenant.EnabledEvents
        });
    }

    /// <summary>
    /// Storefront event batch — JS snippet POST atar.
    /// POST /api/v1/events/storefront
    /// Beacon API text/plain gönderebilir, fetch application/json gönderir.
    /// </summary>
    [HttpPost("storefront")]
    [Consumes("application/json", "text/plain")]
    [ProducesResponseType(typeof(StorefrontEventBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IngestBatch(CancellationToken ct)
    {
        // Body oku — Beacon API text/plain gönderebilir
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { error = "Empty request body" });

        StorefrontEventBatchRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<StorefrontEventBatchRequest>(body, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Storefront event JSON parse error");
            return BadRequest(new { error = "Invalid JSON payload" });
        }

        if (request?.Events is null or { Count: 0 })
            return BadRequest(new { error = "No events in payload" });

        if (request.Events.Count > 20)
            return BadRequest(new { error = "Max 20 events per batch" });

        // ApiKey — body'den veya header'dan
        var apiKey = request.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Request.Headers["X-Profiqo-ApiKey"].FirstOrDefault() ?? "";

        if (string.IsNullOrWhiteSpace(apiKey))
            return Unauthorized(new { error = "Missing apiKey" });

        // Tenant çözümle
        var tenant = await _tenantResolver.ResolveAsync(apiKey, ct);
        if (tenant is null)
        {
            _logger.LogWarning("Invalid pixel apiKey: {ApiKeyPrefix}",
                apiKey.Length > 12 ? apiKey[..12] + "***" : "***");
            return Unauthorized(new { error = "Invalid apiKey" });
        }

        // Device ID hash
        var deviceIdHash = HashDeviceId(request.DeviceId);

        // Event'leri işle
        var result = await _eventService.ProcessBatchAsync(
            tenant.TenantId,
            deviceIdHash,
            request.SessionId,
            request.Customer,
            request.Events,
            GetClientIp(),
            ct);

        _logger.LogInformation(
            "Storefront events ingested. Tenant={TenantId}, Device={DeviceHash}, " +
            "Customer={CustomerEmail}, Accepted={Accepted}, Rejected={Rejected}",
            tenant.TenantId, deviceIdHash[..12],
            request.Customer?.Email ?? "anonymous",
            result.Accepted, result.Rejected);

        return Ok(result);
    }

    [HttpOptions("storefront")]
    public IActionResult PreflightStorefront() => NoContent();

    [HttpOptions("storefront/config")]
    public IActionResult PreflightConfig() => NoContent();

    [HttpGet("storefront/health")]
    [DisableRateLimiting]
    public IActionResult Health() =>
        Ok(new { status = "ok", version = "2.0.0", timestamp = DateTimeOffset.UtcNow });

    private static string HashDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return new string('0', 64);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(deviceId));
        return Convert.ToHexStringLower(bytes);
    }

    private string? GetClientIp()
    {
        var cfIp = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfIp)) return cfIp;
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
