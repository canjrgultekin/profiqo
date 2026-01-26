using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;

namespace Profiqo.Domain.Tenants;



public sealed class Tenant : AggregateRoot<TenantId>
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Tenant() : base()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Tenant(TenantId id, string name, string slug, DateTimeOffset nowUtc) : base(id)
    {
        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 200, nameof(name));
        Slug = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(slug, nameof(slug)), 80, nameof(slug));
        Status = TenantStatus.Active;

        CreatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static Tenant Create(string name, string slug, DateTimeOffset nowUtc)
        => new(TenantId.New(), name, slug, nowUtc);

    public void Rename(string name, DateTimeOffset nowUtc)
    {
        Name = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(name, nameof(name)), 200, nameof(name));
        Touch(nowUtc);
    }

    public void ChangeSlug(string slug, DateTimeOffset nowUtc)
    {
        Slug = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(slug, nameof(slug)), 80, nameof(slug));
        Touch(nowUtc);
    }

    public void Suspend(DateTimeOffset nowUtc)
    {
        if (Status == TenantStatus.Closed)
            throw new BusinessRuleViolationException("tenant_closed", "Closed tenant cannot be suspended.");

        Status = TenantStatus.Suspended;
        Touch(nowUtc);
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        if (Status == TenantStatus.Closed)
            throw new BusinessRuleViolationException("tenant_closed", "Closed tenant cannot be activated.");

        Status = TenantStatus.Active;
        Touch(nowUtc);
    }

    public void Close(DateTimeOffset nowUtc)
    {
        Status = TenantStatus.Closed;
        Touch(nowUtc);
    }

    private void Touch(DateTimeOffset nowUtc)
        => UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
}
