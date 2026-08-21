using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// Régimen tributario del emisor. Determina qué comprobantes SUNAT puede emitir
/// sin enviar una solicitud inválida al PSE/OSE.
/// </summary>
public readonly record struct TaxRegime
{
    public string Code { get; }
    public string Name { get; }

    private TaxRegime(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly TaxRegime Rus = new("rus", "RUS / NRUS");
    public static readonly TaxRegime Rer = new("rer", "Régimen Especial de Renta");
    public static readonly TaxRegime Mype = new("mype", "MYPE Tributario");
    public static readonly TaxRegime General = new("general", "Régimen General");

    public static IReadOnlyList<TaxRegime> All { get; } = [Rus, Rer, Mype, General];

    public bool CanIssue(DocumentType documentType)
    {
        if (this == Rus)
        {
            return documentType == DocumentType.Receipt;
        }

        return true;
    }

    public void EnsureCanIssue(DocumentType documentType)
    {
        if (CanIssue(documentType))
        {
            return;
        }

        throw new BusinessRuleException(
            "TAX_REGIME",
            $"El régimen {Name} solo permite emitir boletas electrónicas. No se envió la solicitud a SUNAT.");
    }

    public IReadOnlyList<DocumentType> AllowedDocumentTypes =>
        AllDocumentTypes.Where(CanIssue).ToArray();

    private static IReadOnlyList<DocumentType> AllDocumentTypes { get; } = DocumentType.All;

    public static TaxRegime FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return General;
        }

        var normalized = code.Trim().ToLowerInvariant();
        foreach (var regime in All)
        {
            if (regime.Code == normalized)
            {
                return regime;
            }
        }

        throw new BusinessRuleException("TAX_REGIME", $"Unknown tax regime '{code}'. Use rus, rer, mype or general.");
    }
}
