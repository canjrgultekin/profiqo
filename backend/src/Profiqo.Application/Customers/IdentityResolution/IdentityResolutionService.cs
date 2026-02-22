// Path: backend/src/Profiqo.Application/Customers/IdentityResolution/IdentityResolutionService.cs
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Persistence.Repositories;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Customers;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Customers.IdentityResolution;

public sealed record IdentityInput(
    IdentityType Type,
    string NormalizedValue,
    IdentityHash Hash,
    ProviderType SourceProvider,
    string? SourceExternalId);

public interface IIdentityResolutionService
{
    Task<CustomerId> ResolveOrCreateCustomerAsync(
        TenantId tenantId,
        string? firstName,
        string? lastName,
        IReadOnlyList<IdentityInput> identities,
        DateTimeOffset nowUtc,
        CancellationToken ct);
}

internal sealed class IdentityResolutionService : IIdentityResolutionService
{
    private readonly ICustomerRepository _customers;
    private readonly ISecretProtector _secrets;

    public IdentityResolutionService(ICustomerRepository customers, ISecretProtector secrets)
    {
        _customers = customers;
        _secrets = secrets;
    }

    public async Task<CustomerId> ResolveOrCreateCustomerAsync(
        TenantId tenantId,
        string? firstName,
        string? lastName,
        IReadOnlyList<IdentityInput> identities,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        // ✅ Deterministic matching: Email -> Phone
        // ✅ KRİTİK: mevcut müşteri bulunduysa Customer entity materialize ETMİYORUZ, sadece CustomerId dönüyoruz.
        // Poison customer row yüzünden ingestion çökmeyecek.

        CustomerId? existingId = null;

        var email = identities.FirstOrDefault(x => x.Type == IdentityType.Email);
        if (email is not null)
            existingId = await _customers.FindIdByIdentityHashAsync(tenantId, IdentityType.Email, email.Hash, ct);

        if (existingId is null)
        {
            var phone = identities.FirstOrDefault(x => x.Type == IdentityType.Phone);
            if (phone is not null)
                existingId = await _customers.FindIdByIdentityHashAsync(tenantId, IdentityType.Phone, phone.Hash, ct);
        }

        if (existingId is not null)
            return existingId.Value;

        // ✅ Create new customer
        var created = Customer.Create(tenantId, nowUtc);

        var fn = NormalizeNameTokenOrNull(firstName);
        var ln = NormalizeNameTokenOrNull(lastName);
        if (fn is not null || ln is not null)
            created.SetName(fn, ln, nowUtc);

        foreach (var i in identities)
            AddIdentity(created, tenantId, i, nowUtc);

        await _customers.AddAsync(created, ct);
        return created.Id;
    }

    private static string? NormalizeNameTokenOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length == 0 ? null : t;
    }

    private void AddIdentity(Customer customer, TenantId tenantId, IdentityInput i, DateTimeOffset nowUtc)
    {
        var enc = _secrets.Protect(i.NormalizedValue);

        var identity = CustomerIdentity.Create(
            tenantId: tenantId,
            type: i.Type,
            valueHash: i.Hash,
            valueEncrypted: enc,
            sourceProvider: i.SourceProvider,
            sourceExternalId: i.SourceExternalId,
            nowUtc: nowUtc);

        customer.AddOrTouchIdentity(identity, nowUtc);
    }
}