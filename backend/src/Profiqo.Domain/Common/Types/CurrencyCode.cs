namespace Profiqo.Domain.Common.Types;

using Profiqo.Domain.Common;

public sealed record CurrencyCode
{
    private static readonly Regex Iso4217 = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public string Value { get; init; }

    private CurrencyCode()
    {
        Value = "TRY";
    }

    public CurrencyCode(string value)
    {
        value = Guard.AgainstNullOrWhiteSpace(value, nameof(value)).ToUpperInvariant();
        if (!Iso4217.IsMatch(value))
            throw new DomainException("CurrencyCode must be a 3-letter ISO code (e.g., TRY, USD, EUR).");

        Value = value;
    }

    public override string ToString() => Value;

    public static CurrencyCode TRY => new("TRY");
    public static CurrencyCode USD => new("USD");
    public static CurrencyCode EUR => new("EUR");
}