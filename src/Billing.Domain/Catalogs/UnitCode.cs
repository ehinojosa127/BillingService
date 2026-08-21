using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 03 — Unidades de medida (UNECE Rec 20 / UN/ECE).
/// </summary>
public static class UnitCode
{
    public const string Unit = "NIU";
    public const string Service = "ZZ";
    public const string Kilogram = "KGM";
    public const string Gram = "GRM";
    public const string Liter = "LTR";
    public const string Meter = "MTR";
    public const string SquareMeter = "MTK";
    public const string CubicMeter = "MTQ";
    public const string Box = "BX";
    public const string Pair = "PR";
    public const string Hour = "HUR";
    public const string Day = "DAY";
    public const string Kilometer = "KMT";
    public const string Ton = "TNE";

    private static readonly HashSet<string> Known =
    [
        Unit, Service, Kilogram, Gram, Liter, Meter, SquareMeter, CubicMeter,
        Box, Pair, Hour, Day, Kilometer, Ton, "GLL", "ONZ", "LBR", "INH", "FOT",
        "YRD", "SET", "DZN", "CEN", "MIL", "PK", "SA", "TU", "BG", "BO", "CT"
    ];

    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessRuleException("UNIT_CODE", "Unit code is required.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (!Known.Contains(normalized))
        {
            throw new BusinessRuleException("UNIT_CODE", $"Unknown SUNAT unit code '{code}'.");
        }

        return normalized;
    }

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Known.Contains(code.Trim().ToUpperInvariant());
}
