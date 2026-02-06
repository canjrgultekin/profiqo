namespace Profiqo.Domain.Common.Ids;


public readonly record struct MessageTemplateId
{
    public Guid Value { get; }

    public MessageTemplateId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static MessageTemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}