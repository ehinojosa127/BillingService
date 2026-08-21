using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 09 (notas de crédito) y Catálogo 10 (notas de débito).
/// </summary>
public readonly record struct NoteReasonCode
{
    public string Code { get; }
    public string Name { get; }
    public bool IsCreditNote { get; }

    private NoteReasonCode(string code, string name, bool isCreditNote)
    {
        Code = code;
        Name = name;
        IsCreditNote = isCreditNote;
    }

    public static readonly NoteReasonCode CreditCancellation = new("01", "Anulación de la operación", true);
    public static readonly NoteReasonCode CreditDiscount = new("02", "Anulación por error en el RUC", true);
    public static readonly NoteReasonCode CreditCorrection = new("03", "Corrección por error en la descripción", true);
    public static readonly NoteReasonCode CreditGlobalDiscount = new("04", "Descuento global", true);
    public static readonly NoteReasonCode CreditItemDiscount = new("05", "Descuento por ítem", true);
    public static readonly NoteReasonCode CreditTotalRefund = new("06", "Devolución total", true);
    public static readonly NoteReasonCode CreditItemRefund = new("07", "Devolución por ítem", true);
    public static readonly NoteReasonCode CreditBonus = new("08", "Bonificación", true);
    public static readonly NoteReasonCode CreditDecrease = new("09", "Disminución en el valor", true);
    public static readonly NoteReasonCode CreditOther = new("10", "Otros conceptos", true);
    public static readonly NoteReasonCode CreditExportAdjust = new("11", "Ajustes de operaciones de exportación", true);
    public static readonly NoteReasonCode CreditIcbperAdjust = new("12", "Ajustes afectos al IVAP", true);

    public static readonly NoteReasonCode DebitInterest = new("01", "Intereses por mora", false);
    public static readonly NoteReasonCode DebitIncrease = new("02", "Aumento en el valor", false);
    public static readonly NoteReasonCode DebitPenalties = new("03", "Penalidades / otros conceptos", false);

    public static IReadOnlyList<NoteReasonCode> CreditNotes { get; } =
    [
        CreditCancellation, CreditDiscount, CreditCorrection, CreditGlobalDiscount, CreditItemDiscount,
        CreditTotalRefund, CreditItemRefund, CreditBonus, CreditDecrease, CreditOther, CreditExportAdjust,
        CreditIcbperAdjust
    ];

    public static IReadOnlyList<NoteReasonCode> DebitNotes { get; } =
    [
        DebitInterest, DebitIncrease, DebitPenalties
    ];

    public static NoteReasonCode ForCreditNote(string code)
    {
        foreach (var item in CreditNotes)
        {
            if (item.Code == code)
            {
                return item;
            }
        }

        throw new BusinessRuleException("NOTE_REASON", $"Unknown credit note reason '{code}'.");
    }

    public static NoteReasonCode ForDebitNote(string code)
    {
        foreach (var item in DebitNotes)
        {
            if (item.Code == code)
            {
                return item;
            }
        }

        throw new BusinessRuleException("NOTE_REASON", $"Unknown debit note reason '{code}'.");
    }

    public override string ToString() => Code;
}
