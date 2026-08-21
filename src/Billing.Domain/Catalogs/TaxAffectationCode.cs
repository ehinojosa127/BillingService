using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 07 — Tipo de afectación del IGV.
/// </summary>
public readonly record struct TaxAffectationCode
{
    public string Code { get; }
    public string Name { get; }
    public string TaxSchemeId { get; }
    public string TaxSchemeName { get; }
    public string TaxTypeCode { get; }
    public bool IsTaxable { get; }
    public bool IsFree { get; }

    private TaxAffectationCode(
        string code,
        string name,
        string taxSchemeId,
        string taxSchemeName,
        string taxTypeCode,
        bool isTaxable,
        bool isFree)
    {
        Code = code;
        Name = name;
        TaxSchemeId = taxSchemeId;
        TaxSchemeName = taxSchemeName;
        TaxTypeCode = taxTypeCode;
        IsTaxable = isTaxable;
        IsFree = isFree;
    }

    public static readonly TaxAffectationCode GravadoOnerosa = new("10", "Gravado - Operación Onerosa", "1000", "IGV", "VAT", true, false);
    public static readonly TaxAffectationCode GravadoRetiro = new("11", "Gravado – Retiro por premio", "9996", "GRA", "FRE", true, true);
    public static readonly TaxAffectationCode ExoneradoOnerosa = new("20", "Exonerado - Operación Onerosa", "9997", "EXO", "VAT", false, false);
    public static readonly TaxAffectationCode ExoneradoTransferenciaGratuita = new("21", "Exonerado – Transferencia Gratuita", "9996", "GRA", "FRE", false, true);
    public static readonly TaxAffectationCode InafectoOnerosa = new("30", "Inafecto - Operación Onerosa", "9998", "INA", "FRE", false, false);
    public static readonly TaxAffectationCode InafectoRetiro = new("31", "Inafecto – Retiro por Bonificación", "9996", "GRA", "FRE", false, true);
    public static readonly TaxAffectationCode Exportacion = new("40", "Exportación", "9995", "EXP", "FRE", false, false);

    public static IReadOnlyList<TaxAffectationCode> All { get; } =
    [
        GravadoOnerosa, GravadoRetiro, ExoneradoOnerosa, ExoneradoTransferenciaGratuita,
        InafectoOnerosa, InafectoRetiro, Exportacion
    ];

    public decimal IgvRate => IsTaxable && !IsFree ? TaxRates.Igv : 0m;

    public static TaxAffectationCode FromCode(string code)
    {
        foreach (var item in All)
        {
            if (item.Code == code)
            {
                return item;
            }
        }

        throw new BusinessRuleException("TAX_AFFECTATION", $"Unknown SUNAT tax affectation code '{code}'.");
    }

    public override string ToString() => Code;
}

public static class TaxRates
{
    public const decimal Igv = 0.18m;
    public const decimal IgvPercent = 18m;
}
