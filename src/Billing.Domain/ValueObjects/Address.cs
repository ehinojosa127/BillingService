using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public sealed record Address
{
    public string Line { get; }
    public string Ubigeo { get; }
    public string Department { get; }
    public string Province { get; }
    public string District { get; }
    public string CountryCode { get; }
    public string? Urbanization { get; }

    public Address(
        string line,
        string ubigeo,
        string department,
        string province,
        string district,
        string countryCode = "PE",
        string? urbanization = null)
    {
        if (string.IsNullOrWhiteSpace(line) || IsUnspecifiedFiscalAddress(line))
        {
            line = "S/N";
        }

        if (string.IsNullOrWhiteSpace(ubigeo) || ubigeo.Trim().Length != 6)
        {
            throw new BusinessRuleException("ADDRESS", "Ubigeo must be 6 digits.");
        }

        Line = line.Trim();
        Ubigeo = ubigeo.Trim();
        Department = Require(department, "Department");
        Province = Require(province, "Province");
        District = Require(district, "District");
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "PE" : countryCode.Trim().ToUpperInvariant();
        Urbanization = string.IsNullOrWhiteSpace(urbanization) ? null : urbanization.Trim();
    }

    private static string Require(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException("ADDRESS", $"{field} is required.");
        }

        return value.Trim();
    }

    private static bool IsUnspecifiedFiscalAddress(string line)
    {
        var normalized = line.Trim();
        return normalized is "-" or "—" or "." or "S/N" or "SIN DOMICILIO";
    }
}
