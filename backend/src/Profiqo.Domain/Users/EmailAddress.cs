using System.Text.RegularExpressions;

namespace Profiqo.Domain.Users;

public sealed record EmailAddress
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; init; }

    private EmailAddress()
    {
        Value = "unknown@example.com";
    }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email is required.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length > 254)
            throw new ArgumentException("Email is too long.", nameof(value));

        if (!EmailRegex.IsMatch(trimmed))
            throw new ArgumentException("Email format is invalid.", nameof(value));

        Value = trimmed.ToLowerInvariant();
    }

    public override string ToString() => Value;
}