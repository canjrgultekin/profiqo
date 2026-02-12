using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Application.Customers.IdentityResolution;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;
using Profiqo.Domain.StorefrontEvents;

namespace Profiqo.Application.StorefrontEvents;

public interface IStorefrontEventService
{
    Task<StorefrontEventBatchResponse> ProcessBatchAsync(
        TenantId tenantId,
        string deviceIdHash,
        string? sessionId,
        StorefrontCustomerContext? customer,
        IReadOnlyList<StorefrontEventItem> events,
        string? clientIp,
        CancellationToken ct);
}

public sealed class StorefrontEventService : IStorefrontEventService
{
    private readonly IWebEventRepository _webEvents;
    private readonly IIdentityResolutionService _identityResolution;
    private readonly ISecretProtector _secrets;
    private readonly ILogger<StorefrontEventService> _logger;

    private static readonly HashSet<string> SupportedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD_TO_CART", "REMOVE_FROM_CART", "COMPLETE_CHECKOUT", "ADD_TO_WISHLIST",
        "PAGE_VIEW", "PRODUCT_VIEW", "BEGIN_CHECKOUT", "SEARCH"
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public StorefrontEventService(
        IWebEventRepository webEvents,
        IIdentityResolutionService identityResolution,
        ISecretProtector secrets,
        ILogger<StorefrontEventService> logger)
    {
        _webEvents = webEvents;
        _identityResolution = identityResolution;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<StorefrontEventBatchResponse> ProcessBatchAsync(
        TenantId tenantId,
        string deviceIdHash,
        string? sessionId,
        StorefrontCustomerContext? customer,
        IReadOnlyList<StorefrontEventItem> events,
        string? clientIp,
        CancellationToken ct)
    {
        var accepted = 0;
        var rejected = 0;
        var errors = new List<string>();
        var inserts = new List<WebEventInsert>();
        var nowUtc = DateTimeOffset.UtcNow;

        // Customer resolution — email/phone ile mevcut müşteri eşleştir veya yeni oluştur
        Guid? resolvedCustomerId = null;
        if (customer is not null && !customer.IsGuest && HasIdentifiable(customer))
        {
            try
            {
                resolvedCustomerId = await ResolveCustomerAsync(tenantId, customer, nowUtc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storefront customer resolution failed for tenant {TenantId}", tenantId);
                // Resolution hatası event ingestion'ı engellemez
            }
        }

        foreach (var evt in events)
        {
            if (string.IsNullOrWhiteSpace(evt.Type) || !SupportedEventTypes.Contains(evt.Type))
            {
                rejected++;
                errors.Add($"Unsupported event type: {evt.Type}");
                continue;
            }

            // Event-level customer override (COMPLETE_CHECKOUT'ta sipariş sahibi farklı olabilir)
            var eventCustomerId = resolvedCustomerId;
            if (evt.Customer is not null && HasIdentifiable(evt.Customer) && !evt.Customer.IsGuest)
            {
                try
                {
                    eventCustomerId = await ResolveCustomerAsync(tenantId, evt.Customer, nowUtc, ct);
                }
                catch { /* batch-level customer'ı kullan */ }
            }

            // Event data — customer bilgisini de ekle
            var eventDataObj = new Dictionary<string, object?>(evt.Data ?? []);
            if (customer is not null)
            {
                eventDataObj["_customer"] = new
                {
                    customer.Id,
                    customer.Email,
                    customer.FirstName,
                    customer.LastName,
                    customer.Phone,
                    customer.IsGuest
                };
            }

            var insert = new WebEventInsert
            {
                Id = Guid.TryParse(evt.EventId, out var eid) ? eid : Guid.NewGuid(),
                TenantId = tenantId.Value,
                EventType = evt.Type.ToUpperInvariant(),
                DeviceIdHash = deviceIdHash,
                SessionId = sessionId,
                CustomerId = eventCustomerId,
                ClientIp = clientIp,
                PageUrl = evt.Page?.Url,
                PagePath = evt.Page?.Path,
                PageReferrer = evt.Page?.Referrer,
                PageTitle = evt.Page?.Title,
                UserAgent = evt.Page?.UserAgent,
                EventDataJson = JsonSerializer.Serialize(eventDataObj, JsonOpts),
                OccurredAtUtc = evt.OccurredAt?.ToUniversalTime() ?? nowUtc,
                CreatedAtUtc = nowUtc
            };

            inserts.Add(insert);
            accepted++;
        }

        if (inserts.Count > 0)
        {
            await _webEvents.InsertBatchAsync(inserts, ct);
        }

        return new StorefrontEventBatchResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            Errors = errors.Count > 0 ? errors : null
        };
    }

    private async Task<Guid?> ResolveCustomerAsync(
        TenantId tenantId,
        StorefrontCustomerContext customer,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var identities = new List<IdentityInput>();

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            var normalized = customer.Email.Trim().ToLowerInvariant();
            var hash = ComputeSha256(normalized);
            identities.Add(new IdentityInput(
                IdentityType.Email, normalized, new IdentityHash(hash),
                ProviderType.Pixel, customer.Id));
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            var normalized = NormalizePhone(customer.Phone);
            var hash = ComputeSha256(normalized);
            identities.Add(new IdentityInput(
                IdentityType.Phone, normalized, new IdentityHash(hash),
                ProviderType.Pixel, customer.Id));
        }

        if (identities.Count == 0)
            return null;

        var customerId = await _identityResolution.ResolveOrCreateCustomerAsync(
            tenantId,
            customer.FirstName,
            customer.LastName,
            identities,
            nowUtc,
            ct);

        return customerId.Value;
    }

    private static bool HasIdentifiable(StorefrontCustomerContext c)
        => !string.IsNullOrWhiteSpace(c.Email) || !string.IsNullOrWhiteSpace(c.Phone);

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return digits.Length > 0 ? digits : phone.Trim();
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
