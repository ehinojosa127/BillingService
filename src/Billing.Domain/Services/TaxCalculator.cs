using Billing.Domain.Catalogs;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Services;

public static class TaxCalculator
{
    public static LineTaxResult CalculateLine(
        decimal quantity,
        decimal unitValue,
        decimal discount,
        TaxAffectationCode affectation,
        string currency)
    {
        var netBase = (quantity * unitValue) - discount;
        if (netBase < 0)
        {
            netBase = 0;
        }

        var igvRate = affectation.IgvRate;
        if (igvRate > 0m && !affectation.IsFree)
        {
            var taxInclusive = Money.Round(netBase * (1 + igvRate));
            var taxable = Money.Round(taxInclusive / (1 + igvRate));
            var igv = taxInclusive - taxable;
            var unitPrice = Money.Round(unitValue * (1 + igvRate));

            return new LineTaxResult(
                LineExtensionAmount: taxable,
                IgvAmount: igv,
                UnitPrice: unitPrice,
                Total: taxInclusive,
                Currency: currency);
        }

        var lineExtension = Money.Round(netBase);
        var unitPriceWithoutIgv = affectation.IsFree
            ? 0m
            : Money.Round(unitValue);

        return new LineTaxResult(
            LineExtensionAmount: lineExtension,
            IgvAmount: 0m,
            UnitPrice: unitPriceWithoutIgv,
            Total: affectation.IsFree ? 0m : lineExtension,
            Currency: currency);
    }

    public static TaxTotals CalculateDocument(IEnumerable<DocumentLineInput> lines, string currency, decimal globalDiscount = 0m, decimal globalCharge = 0m)
    {
        decimal taxable = 0, exempt = 0, unaffected = 0, free = 0, export = 0, igv = 0, lineExtension = 0, discount = 0;

        foreach (var line in lines)
        {
            var result = CalculateLine(line.Quantity, line.UnitValue, line.Discount, line.Affectation, currency);
            lineExtension += result.LineExtensionAmount;
            igv += result.IgvAmount;
            discount += line.Discount;

            if (line.Affectation.IsFree)
            {
                free += result.LineExtensionAmount;
            }
            else if (line.Affectation == TaxAffectationCode.GravadoOnerosa)
            {
                taxable += result.LineExtensionAmount;
            }
            else if (line.Affectation == TaxAffectationCode.ExoneradoOnerosa)
            {
                exempt += result.LineExtensionAmount;
            }
            else if (line.Affectation == TaxAffectationCode.InafectoOnerosa)
            {
                unaffected += result.LineExtensionAmount;
            }
            else if (line.Affectation == TaxAffectationCode.Exportacion)
            {
                export += result.LineExtensionAmount;
            }
        }

        var payableBase = Money.Round(lineExtension - globalDiscount + globalCharge + igv);
        return new TaxTotals
        {
            TaxableAmount = Money.Round(taxable),
            ExemptAmount = Money.Round(exempt),
            UnaffectedAmount = Money.Round(unaffected),
            FreeAmount = Money.Round(free),
            ExportAmount = Money.Round(export),
            IgvAmount = Money.Round(igv),
            LineExtensionAmount = Money.Round(lineExtension),
            TaxInclusiveAmount = Money.Round(lineExtension + igv),
            PayableAmount = payableBase < 0 ? 0 : payableBase,
            DiscountAmount = Money.Round(discount + globalDiscount),
            ChargeAmount = Money.Round(globalCharge)
        };
    }
}

public sealed record DocumentLineInput(
    decimal Quantity,
    decimal UnitValue,
    decimal Discount,
    TaxAffectationCode Affectation);

public sealed record LineTaxResult(
    decimal LineExtensionAmount,
    decimal IgvAmount,
    decimal UnitPrice,
    decimal Total,
    string Currency);
