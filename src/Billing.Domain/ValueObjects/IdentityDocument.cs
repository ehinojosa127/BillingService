using System.Text.RegularExpressions;
using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public sealed partial record IdentityDocument
{
    public IdentityDocumentType Type { get; }
    public string Number { get; }

    public IdentityDocument(IdentityDocumentType type, string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessRuleException("IDENTITY", "Identity document number is required.");
        }

        var trimmed = number.Trim();
        Validate(type, trimmed);
        Type = type;
        Number = trimmed;
    }

    private static void Validate(IdentityDocumentType type, string number)
    {
        if (type.IsRuc)
        {
            if (!Ruc.IsValid(number))
            {
                throw new BusinessRuleException("IDENTITY", $"Invalid RUC '{number}'.");
            }

            return;
        }

        if (type.IsDni && !DniPattern().IsMatch(number))
        {
            throw new BusinessRuleException("IDENTITY", $"Invalid DNI '{number}'.");
        }
    }

    public override string ToString() => $"{Type.Code}-{Number}";

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex DniPattern();
}
