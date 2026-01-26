namespace Profiqo.Domain.Common.Ids;


public readonly record struct TenantId
{
    public Guid Value { get; }

    public TenantId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static TenantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}