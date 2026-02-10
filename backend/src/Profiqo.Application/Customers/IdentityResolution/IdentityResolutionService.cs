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
        // Deterministic matching order: Email -> Phone
        Customer? existing = null;

        var email = identities.FirstOrDefault(x => x.Type == IdentityType.Email);
        if (email is not null)
            existing = await _customers.FindByIdentityHashAsync(tenantId, IdentityType.Email, email.Hash, ct);

        if (existing is null)
        {
            var phone = identities.FirstOrDefault(x => x.Type == IdentityType.Phone);
            if (phone is not null)
                existing = await _customers.FindByIdentityHashAsync(tenantId, IdentityType.Phone, phone.Hash, ct);
        }

        if (existing is null)
        {
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

        // Update name if needed (non-destructive, field-level)
        var inFirst = NormalizeNameTokenOrNull(firstName);
        var inLast = NormalizeNameTokenOrNull(lastName);

        if (inFirst is not null || inLast is not null)
        {
            var nextFirst = inFirst ?? existing.FirstName;
            var nextLast = inLast ?? existing.LastName;

            if (nextFirst != existing.FirstName || nextLast != existing.LastName)
                existing.SetName(nextFirst, nextLast, nowUtc);
        }

        foreach (var i in identities)
            AddIdentity(existing, tenantId, i, nowUtc);

        return existing.Id;
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
