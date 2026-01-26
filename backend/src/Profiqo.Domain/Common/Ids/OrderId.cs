namespace Profiqo.Domain.Common.Ids;


public readonly record struct OrderId
{
    public Guid Value { get; }

    public OrderId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}