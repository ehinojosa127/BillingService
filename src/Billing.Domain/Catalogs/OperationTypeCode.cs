using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 51 — Tipo de operación.
/// </summary>
public readonly record struct OperationTypeCode
{
    public string Code { get; }
    public string Name { get; }

    private OperationTypeCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly OperationTypeCode InternalSale = new("0101", "Venta interna");
    public static readonly OperationTypeCode Export = new("0102", "Exportación");
    public static readonly OperationTypeCode NonDomiciled = new("0103", "No domiciliados");
    public static readonly OperationTypeCode InternalSaleAdvances = new("0104", "Venta interna – Anticipos");
    public static readonly OperationTypeCode ItinerantSale = new("0105", "Venta itinerante");
    public static readonly OperationTypeCode InvoiceGuide = new("0106", "Factura — Guía");
    public static readonly OperationTypeCode SaleRicePill = new("0107", "Venta arroz pilado");
    public static readonly OperationTypeCode PerceptionInternalSale = new("0108", "Factura — Comisión de recaudo");
    public static readonly OperationTypeCode ServiceExport = new("0110", "Exportación de servicios");

    public static IReadOnlyList<OperationTypeCode> All { get; } =
    [
        InternalSale, Export, NonDomiciled, InternalSaleAdvances, ItinerantSale,
        InvoiceGuide, SaleRicePill, PerceptionInternalSale, ServiceExport
    ];

    public static OperationTypeCode FromCode(string code)
    {
        foreach (var item in All)
        {
            if (item.Code == code)
            {
                return item;
            }
        }

        throw new BusinessRuleException("OPERATION_TYPE", $"Unknown SUNAT operation type '{code}'.");
    }

    public override string ToString() => Code;
}
