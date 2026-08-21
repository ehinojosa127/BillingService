using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 01 — Tipo de documento.
/// </summary>
public readonly record struct DocumentType
{
    public string Code { get; }
    public string Name { get; }

    private DocumentType(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly DocumentType Invoice = new("01", "Factura");
    public static readonly DocumentType Receipt = new("03", "Boleta");
    public static readonly DocumentType CreditNote = new("07", "Nota de crédito");
    public static readonly DocumentType DebitNote = new("08", "Nota de débito");
    public static readonly DocumentType ShippingGuide = new("09", "Guía de remisión remitente");

    public static IReadOnlyList<DocumentType> All { get; } =
    [
        Invoice, Receipt, CreditNote, DebitNote, ShippingGuide
    ];

    public bool IsInvoiceOrReceipt => this == Invoice || this == Receipt;
    public bool IsNote => this == CreditNote || this == DebitNote;
    public bool RequiresRelatedDocument => IsNote;
    public bool IsShippingGuide => this == ShippingGuide;

    public char RequiredSeriesPrefix => this switch
    {
        _ when this == Invoice => 'F',
        _ when this == Receipt => 'B',
        _ when this == ShippingGuide => 'T',
        _ => throw new BusinessRuleException("SERIES_PREFIX", $"Document type {Code} does not have a unique series prefix.")
    };

    public static DocumentType FromCode(string code)
    {
        foreach (var type in All)
        {
            if (type.Code == code)
            {
                return type;
            }
        }

        throw new BusinessRuleException("DOCUMENT_TYPE", $"Unknown SUNAT document type '{code}'.");
    }

    public static bool TryFromCode(string? code, out DocumentType type)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            foreach (var candidate in All)
            {
                if (candidate.Code == code)
                {
                    type = candidate;
                    return true;
                }
            }
        }

        type = default;
        return false;
    }

    public override string ToString() => Code;
}
