using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 18 — Modalidad de transporte.
/// </summary>
public readonly record struct TransportModeCode
{
    public string Code { get; }
    public string Name { get; }

    private TransportModeCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly TransportModeCode Public = new("01", "Transporte público");
    public static readonly TransportModeCode Private = new("02", "Transporte privado");

    public static TransportModeCode FromCode(string code)
    {
        if (code == Public.Code)
        {
            return Public;
        }

        if (code == Private.Code)
        {
            return Private;
        }

        throw new BusinessRuleException("TRANSPORT_MODE", $"Unknown SUNAT transport mode '{code}'.");
    }

    public override string ToString() => Code;
}
