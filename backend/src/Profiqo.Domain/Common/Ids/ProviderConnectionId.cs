namespace Profiqo.Domain.Common.Ids;


public readonly record struct ProviderConnectionId
{
    public Guid Value { get; }

    public ProviderConnectionId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static ProviderConnectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}