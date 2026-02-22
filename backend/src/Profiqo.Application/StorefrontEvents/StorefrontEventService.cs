// Path: backend/src/Profiqo.Application/StorefrontEvents/StorefrontEventService.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Customers.IdentityResolution;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;

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
    private readonly IStorefrontCheckoutProjector _checkoutProjector;
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
        IStorefrontCheckoutProjector checkoutProjector,
        ILogger<StorefrontEventService> logger)
    {
        _webEvents = webEvents;
        _identityResolution = identityResolution;
        _secrets = secrets;
        _checkoutProjector = checkoutProjector;
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
            }
        }

        var checkoutProjections = new List<(Guid? customerId, string eventDataJson, DateTimeOffset occurredAtUtc)>();

        foreach (var evt in events)
        {
            if (string.IsNullOrWhiteSpace(evt.Type) || !SupportedEventTypes.Contains(evt.Type))
            {
                rejected++;
                errors.Add($"Unsupported event type: {evt.Type}");
                continue;
            }

            // Event-level customer override
            var eventCustomerId = resolvedCustomerId;
            StorefrontCustomerContext? effectiveCustomer = customer;

            if (evt.Customer is not null)
            {
                effectiveCustomer = evt.Customer;

                if (!evt.Customer.IsGuest && HasIdentifiable(evt.Customer))
                {
                    try
                    {
                        eventCustomerId = await ResolveCustomerAsync(tenantId, evt.Customer, nowUtc, ct);
                    }
                    catch
                    {
                        // ignore, batch resolvedCustomerId kalır
                    }
                }
            }

            // ✅ EventData içine yazacağımız _customer, effectiveCustomer olmalı (evt.Customer varsa onu bas)
            var eventDataObj = new Dictionary<string, object?>(evt.Data ?? []);

            if (effectiveCustomer is not null)
            {
                eventDataObj["_customer"] = new
                {
                    effectiveCustomer.Id,
                    effectiveCustomer.Email,
                    effectiveCustomer.FirstName,
                    effectiveCustomer.LastName,
                    effectiveCustomer.Phone,
                    effectiveCustomer.IsGuest
                };
            }

            var eventDataJson = JsonSerializer.Serialize(eventDataObj, JsonOpts);
            var occurredAt = evt.OccurredAt?.ToUniversalTime() ?? nowUtc;

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
                EventDataJson = eventDataJson,
                OccurredAtUtc = occurredAt,
                CreatedAtUtc = nowUtc
            };

            inserts.Add(insert);
            accepted++;

            if (insert.EventType == "COMPLETE_CHECKOUT")
                checkoutProjections.Add((eventCustomerId, eventDataJson, occurredAt));
        }

        if (inserts.Count > 0)
            await _webEvents.InsertBatchAsync(inserts, ct);

        foreach (var p in checkoutProjections)
        {
            try
            {
                await _checkoutProjector.ProjectCompleteCheckoutAsync(tenantId, p.customerId, p.eventDataJson, p.occurredAtUtc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Checkout projection failed. Tenant={TenantId}", tenantId);
            }
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