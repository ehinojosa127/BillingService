using System.Text.RegularExpressions;
using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public readonly partial record struct Ruc
{
    public string Value { get; }

    public Ruc(string value)
    {
        if (!IsValid(value))
        {
            throw new BusinessRuleException("RUC", $"Invalid RUC '{value}'.");
        }

        Value = value.Trim();
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !RucPattern().IsMatch(value.Trim()))
        {
            return false;
        }

        var digits = value.Trim();
        var factors = new[] { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (digits[i] - '0') * factors[i];
        }

        var remainder = 11 - (sum % 11);
        var check = remainder is 10 or 11 ? 0 : remainder;
        return check == digits[10] - '0';
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex RucPattern();
}
