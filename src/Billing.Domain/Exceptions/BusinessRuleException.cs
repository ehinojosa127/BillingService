namespace Billing.Domain.Exceptions;

public sealed class BusinessRuleException : DomainException
{
    public string Code { get; }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }
}
