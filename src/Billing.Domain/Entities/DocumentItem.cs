using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;
using Billing.Domain.Services;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Entities;

public sealed class DocumentItem
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int LineNumber { get; private set; }
    public string? Code { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitCode { get; private set; } = Catalogs.UnitCode.Unit;
    public decimal UnitValue { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Discount { get; private set; }
    public string AffectationCode { get; private set; } = TaxAffectationCode.GravadoOnerosa.Code;
    public decimal TaxableAmount { get; private set; }
    public decimal IgvAmount { get; private set; }
    public decimal Total { get; private set; }

    private DocumentItem()
    {
    }

    public static DocumentItem Create(
        Guid documentId,
        int lineNumber,
        string? code,
        string description,
        decimal quantity,
        string unitCode,
        decimal unitValue,
        decimal discount,
        TaxAffectationCode affectation,
        string currency)
    {
        if (lineNumber < 1)
        {
            throw new BusinessRuleException("ITEM", "Line number must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BusinessRuleException("ITEM", "Item description is required.");
        }

        if (quantity <= 0)
        {
            throw new BusinessRuleException("ITEM", "Quantity must be greater than zero.");
        }

        if (unitValue < 0)
        {
            throw new BusinessRuleException("ITEM", "Unit value cannot be negative.");
        }

        if (discount < 0)
        {
            throw new BusinessRuleException("ITEM", "Discount cannot be negative.");
        }

        var tax = TaxCalculator.CalculateLine(quantity, unitValue, discount, affectation, currency);

        return new DocumentItem
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            LineNumber = lineNumber,
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            Description = description.Trim(),
            Quantity = quantity,
            UnitCode = Catalogs.UnitCode.Normalize(unitCode),
            UnitValue = decimal.Round(unitValue, 6, MidpointRounding.AwayFromZero),
            UnitPrice = tax.UnitPrice,
            Discount = Money.Round(discount),
            AffectationCode = affectation.Code,
            TaxableAmount = tax.LineExtensionAmount,
            IgvAmount = tax.IgvAmount,
            Total = tax.Total
        };
    }

    public TaxAffectationCode Affectation => TaxAffectationCode.FromCode(AffectationCode);
}
