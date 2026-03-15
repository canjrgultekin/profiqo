namespace Profiqo.Domain.Customers;

using Profiqo.Domain.Common;

public sealed record IdentityHash
{
    private static readonly Regex Hex64 = new("^[a-f0-9]{64}$", RegexOptions.Compiled);

    public string Value { get; init; }

    private IdentityHash()
    {
        Value = new string('0', 64);
    }

    public IdentityHash(string value)
    {
        value = Guard.AgainstNullOrWhiteSpace(value, nameof(value)).ToLowerInvariant();
        if (!Hex64.IsMatch(value))
            throw new DomainException("IdentityHash must be a lowercase 64-char hex string (sha256).");

        Value = value;
    }

    public override string ToString() => Value;
}