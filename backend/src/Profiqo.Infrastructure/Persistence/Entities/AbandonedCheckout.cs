namespace Profiqo.Infrastructure.Persistence.Entities;

public sealed class AbandonedCheckout
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public short ProviderType { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;

    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }

    public long LastActivityDateMs { get; private set; }
    public string? CurrencyCode { get; private set; }
    public decimal? TotalFinalPrice { get; private set; }
    public string? Status { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AbandonedCheckout() { }

    public AbandonedCheckout(
        Guid id,
        Guid tenantId,
        short providerType,
        string externalId,
        string? customerEmail,
        string? customerPhone,
        long lastActivityDateMs,
        string? currencyCode,
        decimal? totalFinalPrice,
        string? status,
        string payloadJson,
        DateTimeOffset nowUtc)
    {
        Id = id;
        TenantId = tenantId;
        ProviderType = providerType;
        ExternalId = externalId;

        CustomerEmail = customerEmail;
        CustomerPhone = customerPhone;

        LastActivityDateMs = lastActivityDateMs;
        CurrencyCode = currencyCode;
        TotalFinalPrice = totalFinalPrice;
        Status = status;

        PayloadJson = payloadJson;

        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(
        string? customerEmail,
        string? customerPhone,
        long lastActivityDateMs,
        string? currencyCode,
        decimal? totalFinalPrice,
        string? status,
        string payloadJson,
        DateTimeOffset nowUtc)
    {
        CustomerEmail = customerEmail;
        CustomerPhone = customerPhone;

        LastActivityDateMs = lastActivityDateMs;
        CurrencyCode = currencyCode;
        TotalFinalPrice = totalFinalPrice;
        Status = status;

        PayloadJson = payloadJson;
        UpdatedAtUtc = nowUtc;
    }
}
