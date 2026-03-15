namespace Profiqo.Domain.Common.Ids;

public readonly record struct WhatsappTemplateId
{
    public Guid Value { get; }

    public WhatsappTemplateId(Guid value)
    {
        Value = Guard.AgainstEmpty(value, nameof(value));
    }

    public static WhatsappTemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}