namespace Billing.Domain.ValueObjects;

public sealed record TaxTotals
{
    public decimal TaxableAmount { get; init; }
    public decimal ExemptAmount { get; init; }
    public decimal UnaffectedAmount { get; init; }
    public decimal FreeAmount { get; init; }
    public decimal ExportAmount { get; init; }
    public decimal IgvAmount { get; init; }
    public decimal LineExtensionAmount { get; init; }
    public decimal TaxInclusiveAmount { get; init; }
    public decimal PayableAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal ChargeAmount { get; init; }

    public static TaxTotals Empty() => new();
}
