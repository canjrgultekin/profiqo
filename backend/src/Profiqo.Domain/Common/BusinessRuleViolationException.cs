namespace Profiqo.Domain.Common;

public sealed class BusinessRuleViolationException : DomainException
{
    public string Code { get; }

    public BusinessRuleViolationException(string code, string message) : base(message)
    {
        Code = Guard.AgainstNullOrWhiteSpace(code, nameof(code));
    }
}