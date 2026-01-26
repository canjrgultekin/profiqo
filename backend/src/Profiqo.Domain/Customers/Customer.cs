using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Customers;

public sealed class Customer : AggregateRoot<CustomerId>
{
    public TenantId TenantId { get; private set; }

    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }

    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public CustomerRfm? Rfm { get; private set; }
    public CustomerAiScores? AiScores { get; private set; }

    private readonly List<CustomerIdentity> _identities = new();
    public IReadOnlyCollection<CustomerIdentity> Identities => _identities.AsReadOnly();

    private readonly List<CustomerConsent> _consents = new();
    public IReadOnlyCollection<CustomerConsent> Consents => _consents.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Customer() : base()
    {
        TenantId = default;
        FirstSeenAtUtc = default;
        LastSeenAtUtc = default;
    }

    private Customer(CustomerId id, TenantId tenantId, DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;

        var utc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        FirstSeenAtUtc = utc;
        LastSeenAtUtc = utc;

        CreatedAtUtc = utc;
        UpdatedAtUtc = utc;
    }

    public static Customer Create(TenantId tenantId, DateTimeOffset nowUtc)
        => new(CustomerId.New(), tenantId, nowUtc);

    public void SetName(string? firstName, string? lastName, DateTimeOffset nowUtc)
    {
        FirstName = firstName is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(firstName, nameof(firstName)), 100, nameof(firstName));
        LastName = lastName is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(lastName, nameof(lastName)), 100, nameof(lastName));
        Touch(nowUtc);
    }

    public void AddOrTouchIdentity(CustomerIdentity identity, DateTimeOffset nowUtc)
    {
        if (identity is null) throw new DomainException("identity cannot be null.");

        var existing = _identities.FirstOrDefault(x => x.Type == identity.Type && x.ValueHash.Equals(identity.ValueHash));
        if (existing is not null)
        {
            existing.Touch(nowUtc);
            Touch(nowUtc);
            return;
        }

        _identities.Add(identity);
        Touch(nowUtc);
    }

    public void SetConsent(CustomerConsent consent, DateTimeOffset nowUtc)
    {
        if (consent is null) throw new DomainException("consent cannot be null.");

        _consents.RemoveAll(x => x.Type == consent.Type);
        _consents.Add(consent);

        Touch(nowUtc);
    }

    public void SetRfm(CustomerRfm rfm, DateTimeOffset nowUtc)
    {
        if (rfm is null) throw new DomainException("rfm cannot be null.");
        Rfm = rfm;
        Touch(nowUtc);
    }

    public void SetAiScores(CustomerAiScores scores, DateTimeOffset nowUtc)
    {
        if (scores is null) throw new DomainException("scores cannot be null.");
        AiScores = scores;
        Touch(nowUtc);
    }

    public void SeenNow(DateTimeOffset nowUtc)
    {
        LastSeenAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = LastSeenAtUtc;
    }

    private void Touch(DateTimeOffset nowUtc)
    {
        var utc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = utc;
        LastSeenAtUtc = utc;
    }
}
