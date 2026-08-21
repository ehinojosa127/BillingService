using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public readonly record struct DocumentNumber
{
    public int Value { get; }

    public DocumentNumber(int value)
    {
        if (value is < 1 or > 99_999_999)
        {
            throw new BusinessRuleException("NUMBER", "Document number must be between 1 and 99999999.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
