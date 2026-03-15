using Profiqo.Domain.Common;

namespace Profiqo.Domain.Customers;


public sealed class CustomerConsent
{
    public ConsentType Type { get; private set; }
    public ConsentStatus Status { get; private set; }

    public string Source { get; private set; }
    public string PolicyVersion { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    private CustomerConsent()
    {
        Source = string.Empty;
        PolicyVersion = string.Empty;
    }

    private CustomerConsent(
        ConsentType type,
        ConsentStatus status,
        string source,
        string policyVersion,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset changedAtUtc)
    {
        Type = type;
        Status = status;

        Source = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(source, nameof(source)), 200, nameof(source));
        PolicyVersion = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(policyVersion, nameof(policyVersion)), 64, nameof(policyVersion));

        IpAddress = ipAddress is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(ipAddress, nameof(ipAddress)), 64, nameof(ipAddress));
        UserAgent = userAgent is null ? null : Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(userAgent, nameof(userAgent)), 512, nameof(userAgent));

        ChangedAtUtc = Guard.EnsureUtc(changedAtUtc, nameof(changedAtUtc));
    }

    public static CustomerConsent Grant(
        ConsentType type,
        string source,
        string policyVersion,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset changedAtUtc)
    {
        return new CustomerConsent(type, ConsentStatus.Granted, source, policyVersion, ipAddress, userAgent, changedAtUtc);
    }

    public static CustomerConsent Revoke(
        ConsentType type,
        string source,
        string policyVersion,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset changedAtUtc)
    {
        return new CustomerConsent(type, ConsentStatus.Revoked, source, policyVersion, ipAddress, userAgent, changedAtUtc);
    }
}
