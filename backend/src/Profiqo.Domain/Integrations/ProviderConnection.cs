using Profiqo.Domain.Common;
using Profiqo.Domain.Common.Ids;
using Profiqo.Domain.Common.Types;

namespace Profiqo.Domain.Integrations;

public sealed class ProviderConnection : AggregateRoot<ProviderConnectionId>
{
    public TenantId TenantId { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public ProviderConnectionStatus Status { get; private set; }

    public string DisplayName { get; private set; }
    public string? ExternalAccountId { get; private set; }

    public EncryptedSecret AccessToken { get; private set; }
    public EncryptedSecret? RefreshToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }

    public bool IsTestMode { get; private set; } // ✅ NEW

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private ProviderConnection() : base()
    {
        TenantId = default;
        DisplayName = string.Empty;
        AccessToken = default;
    }

    private ProviderConnection(
        ProviderConnectionId id,
        TenantId tenantId,
        ProviderType providerType,
        string displayName,
        string? externalAccountId,
        EncryptedSecret accessToken,
        EncryptedSecret? refreshToken,
        DateTimeOffset? accessTokenExpiresAtUtc,
        bool isTestMode,
        DateTimeOffset nowUtc) : base(id)
    {
        TenantId = tenantId;
        ProviderType = providerType;

        DisplayName = Guard.AgainstTooLong(
            Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName)), 200, nameof(displayName));

        ExternalAccountId = externalAccountId is null
            ? null
            : Guard.AgainstTooLong(
                Guard.AgainstNullOrWhiteSpace(externalAccountId, nameof(externalAccountId)), 200, nameof(externalAccountId));

        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc?.ToUniversalTime();

        IsTestMode = isTestMode;

        Status = ProviderConnectionStatus.Active;

        CreatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static ProviderConnection Create(
        TenantId tenantId,
        ProviderType providerType,
        string displayName,
        string? externalAccountId,
        EncryptedSecret accessToken,
        EncryptedSecret? refreshToken,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset nowUtc,
        bool isTestMode = false) // ✅ default, callsites bozulmaz
    {
        return new ProviderConnection(
            ProviderConnectionId.New(),
            tenantId,
            providerType,
            displayName,
            externalAccountId,
            accessToken,
            refreshToken,
            accessTokenExpiresAtUtc,
            isTestMode,
            nowUtc);
    }

    public void Pause(DateTimeOffset nowUtc)
    {
        Status = ProviderConnectionStatus.Paused;
        Touch(nowUtc);
    }

    public void Resume(DateTimeOffset nowUtc)
    {
        Status = ProviderConnectionStatus.Active;
        Touch(nowUtc);
    }

    public void MarkInvalidCredentials(DateTimeOffset nowUtc)
    {
        Status = ProviderConnectionStatus.InvalidCredentials;
        Touch(nowUtc);
    }

    public void UpdateProfile(string displayName, string? externalAccountId, DateTimeOffset nowUtc)
    {
        DisplayName = Guard.AgainstTooLong(
            Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName)), 200, nameof(displayName));

        ExternalAccountId = externalAccountId is null
            ? null
            : Guard.AgainstTooLong(
                Guard.AgainstNullOrWhiteSpace(externalAccountId, nameof(externalAccountId)), 200, nameof(externalAccountId));

        Touch(nowUtc);
    }

    public void RotateTokens(
        EncryptedSecret accessToken,
        EncryptedSecret? refreshToken,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc?.ToUniversalTime();

        if (Status == ProviderConnectionStatus.InvalidCredentials)
            Status = ProviderConnectionStatus.Active;

        Touch(nowUtc);
    }

    public void SetTestMode(bool isTestMode, DateTimeOffset nowUtc) // ✅ NEW
    {
        IsTestMode = isTestMode;
        Touch(nowUtc);
    }

    private void Touch(DateTimeOffset nowUtc)
        => UpdatedAtUtc = Guard.EnsureUtc(nowUtc, nameof(nowUtc));
}
