using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// Tipo de contribuyente del emisor (ficha RUC).
/// </summary>
public readonly record struct TaxpayerType
{
    public string Code { get; }
    public string Name { get; }

    private TaxpayerType(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly TaxpayerType Natural = new("natural", "Persona natural");
    public static readonly TaxpayerType NaturalWithBusiness = new("natural_with_business", "Persona natural con negocio");
    public static readonly TaxpayerType Legal = new("legal", "Persona jurídica");

    public static IReadOnlyList<TaxpayerType> All { get; } = [Natural, NaturalWithBusiness, Legal];

    public static TaxpayerType FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Legal;
        }

        var normalized = code.Trim().ToLowerInvariant();
        foreach (var type in All)
        {
            if (type.Code == normalized)
            {
                return type;
            }
        }

        throw new BusinessRuleException(
            "TAXPAYER_TYPE",
            $"Unknown taxpayer type '{code}'. Use natural, natural_with_business or legal.");
    }
}
