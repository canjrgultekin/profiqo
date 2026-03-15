namespace Profiqo.Domain.Common.Types;

using Profiqo.Domain.Common;

public sealed record Money
{
    public decimal Amount { get; init; }
    public CurrencyCode Currency { get; init; }

    private Money()
    {
        Amount = 0m;
        Currency = CurrencyCode.TRY;
    }

    public Money(decimal amount, CurrencyCode currency)
    {
        Currency = currency ?? throw new DomainException("Currency cannot be null.");
        Amount = amount;
    }

    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
        => new Money(Amount * factor, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency.Value, other.Currency.Value, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Currency mismatch: {Currency} vs {other.Currency}.");
    }

    public override string ToString()
        => $"{Amount:0.##} {Currency.Value}";
}