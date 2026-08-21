using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 20 — Motivo de traslado (guía de remisión).
/// </summary>
public readonly record struct TransferReasonCode
{
    public string Code { get; }
    public string Name { get; }

    private TransferReasonCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly TransferReasonCode Sale = new("01", "Venta");
    public static readonly TransferReasonCode SaleToConfirm = new("14", "Venta sujeta a confirmar");
    public static readonly TransferReasonCode Purchase = new("02", "Compra");
    public static readonly TransferReasonCode Consignment = new("04", "Traslado entre establecimientos de la misma empresa");
    public static readonly TransferReasonCode Return = new("06", "Devolución");
    public static readonly TransferReasonCode Import = new("08", "Importación");
    public static readonly TransferReasonCode Export = new("09", "Exportación");
    public static readonly TransferReasonCode Other = new("13", "Otros");

    public static IReadOnlyList<TransferReasonCode> All { get; } =
    [
        Sale, Purchase, Consignment, Return, Import, Export, Other, SaleToConfirm
    ];

    public static TransferReasonCode FromCode(string code)
    {
        foreach (var item in All)
        {
            if (item.Code == code)
            {
                return item;
            }
        }

        throw new BusinessRuleException("TRANSFER_REASON", $"Unknown SUNAT transfer reason '{code}'.");
    }

    public override string ToString() => Code;
}
