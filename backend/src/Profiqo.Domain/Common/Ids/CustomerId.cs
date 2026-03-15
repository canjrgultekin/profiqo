namespace Profiqo.Domain.Common.Ids;


public readonly record struct CustomerId
{
    public Guid Value { get; }

    public CustomerId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static CustomerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}