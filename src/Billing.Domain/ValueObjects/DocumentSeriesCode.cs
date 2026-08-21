using System.Text.RegularExpressions;
using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public readonly partial record struct DocumentSeriesCode
{
    public string Value { get; }

    public DocumentSeriesCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !SeriesPattern().IsMatch(value.Trim().ToUpperInvariant()))
        {
            throw new BusinessRuleException("SERIES", $"Invalid document series '{value}'. Expected 4 alphanumeric characters.");
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public char Prefix => Value[0];

    public void EnsureCompatibleWith(DocumentType type, DocumentType? relatedDocumentType = null)
    {
        if (type.IsNote)
        {
            if (relatedDocumentType is null)
            {
                throw new BusinessRuleException("SERIES", "A related document type is required to validate a note series.");
            }

            var expected = relatedDocumentType.Value.RequiredSeriesPrefix;
            if (Prefix != expected)
            {
                throw new BusinessRuleException(
                    "SERIES",
                    $"Note series '{Value}' must start with '{expected}' because the related document is {relatedDocumentType.Value.Name}.");
            }

            return;
        }

        var required = type.RequiredSeriesPrefix;
        if (Prefix != required)
        {
            throw new BusinessRuleException(
                "SERIES",
                $"Series '{Value}' is not valid for {type.Name}. It must start with '{required}'.");
        }
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Z0-9]{4}$")]
    private static partial Regex SeriesPattern();
}
