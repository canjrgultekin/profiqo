using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;
using Profiqo.Domain.Integrations;

namespace Profiqo.Domain.Customers;

public sealed class CustomerIdentity
{
    public TenantId TenantId { get; private set; }
    public IdentityType Type { get; private set; }
    public IdentityHash ValueHash { get; private set; }
    public EncryptedSecret? ValueEncrypted { get; private set; }

    public ProviderType? SourceProvider { get; private set; }
    public string? SourceExternalId { get; private set; }

    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    private CustomerIdentity()
    {
        TenantId = default;
        Type = default;
        ValueHash = new IdentityHash(new string('0', 64));
    }

    private CustomerIdentity(
        TenantId tenantId,
        IdentityType type,
        IdentityHash valueHash,
        EncryptedSecret? valueEncrypted,
        ProviderType? sourceProvider,
        string? sourceExternalId,
        DateTimeOffset nowUtc)
    {
        TenantId = tenantId;
        Type = type;
        ValueHash = valueHash;
        ValueEncrypted = valueEncrypted;
        SourceProvider = sourceProvider;
        SourceExternalId = sourceExternalId;

        var utc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        FirstSeenAtUtc = utc;
        LastSeenAtUtc = utc;
    }

    public static CustomerIdentity Create(
        TenantId tenantId,
        IdentityType type,
        IdentityHash valueHash,
        EncryptedSecret? valueEncrypted,
        ProviderType? sourceProvider,
        string? sourceExternalId,
        DateTimeOffset nowUtc)
        => new(tenantId, type, valueHash, valueEncrypted, sourceProvider, sourceExternalId, nowUtc);

    public void Touch(DateTimeOffset nowUtc)
        => LastSeenAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
}