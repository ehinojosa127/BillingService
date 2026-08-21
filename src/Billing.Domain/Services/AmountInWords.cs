using System.Text;

namespace Billing.Domain.Services;

public static class AmountInWords
{
    private static readonly string[] Units =
    [
        "CERO", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE",
        "DIECIOCHO", "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS",
        "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    private static readonly string[] Tens =
    [
        "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    ];

    private static readonly string[] Hundreds =
    [
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    ];

    public static string ForCurrency(decimal amount, string currencyCode)
    {
        var currencyName = currencyCode.ToUpperInvariant() switch
        {
            "PEN" => "SOLES",
            "USD" => "DÓLARES AMERICANOS",
            "EUR" => "EUROS",
            _ => currencyCode
        };

        var integer = (long)Math.Truncate(amount);
        var cents = (int)Math.Round((amount - integer) * 100m, MidpointRounding.AwayFromZero);
        if (cents == 100)
        {
            integer++;
            cents = 0;
        }

        var words = ConvertInteger(integer);
        return $"SON {words} CON {cents:00}/100 {currencyName}";
    }

    private static string ConvertInteger(long value)
    {
        if (value == 0)
        {
            return "CERO";
        }

        var builder = new StringBuilder();
        var millions = value / 1_000_000;
        var thousands = (value % 1_000_000) / 1_000;
        var remainder = value % 1_000;

        if (millions > 0)
        {
            builder.Append(millions == 1 ? "UN MILLÓN" : $"{ConvertHundreds((int)millions)} MILLONES");
        }

        if (thousands > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(thousands == 1 ? "MIL" : $"{ConvertHundreds((int)thousands)} MIL");
        }

        if (remainder > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(ConvertHundreds((int)remainder));
        }

        return builder.ToString();
    }

    private static string ConvertHundreds(int value)
    {
        if (value == 100)
        {
            return "CIEN";
        }

        var hundred = value / 100;
        var rest = value % 100;
        var parts = new List<string>();

        if (hundred > 0)
        {
            parts.Add(Hundreds[hundred]);
        }

        if (rest > 0)
        {
            parts.Add(ConvertTens(rest));
        }

        return string.Join(' ', parts);
    }

    private static string ConvertTens(int value)
    {
        if (value < 30)
        {
            return Units[value];
        }

        var ten = value / 10;
        var unit = value % 10;
        return unit == 0 ? Tens[ten] : $"{Tens[ten]} Y {Units[unit]}";
    }
}
