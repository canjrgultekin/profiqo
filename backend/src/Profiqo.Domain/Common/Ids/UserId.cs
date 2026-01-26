namespace Profiqo.Domain.Common.Ids;


public readonly record struct UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}