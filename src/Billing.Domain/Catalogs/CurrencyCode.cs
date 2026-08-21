using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 02 — Monedas (ISO 4217).
/// </summary>
public readonly record struct CurrencyCode
{
    public string Code { get; }
    public string Name { get; }

    private CurrencyCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly CurrencyCode Pen = new("PEN", "Sol");
    public static readonly CurrencyCode Usd = new("USD", "US Dollar");
    public static readonly CurrencyCode Eur = new("EUR", "Euro");

    public static IReadOnlyList<CurrencyCode> All { get; } = [Pen, Usd, Eur];

    public static CurrencyCode FromCode(string code)
    {
        foreach (var currency in All)
        {
            if (string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return currency;
            }
        }

        throw new BusinessRuleException("CURRENCY", $"Unsupported currency '{code}'.");
    }

    public override string ToString() => Code;
}
