using Billing.Domain.Exceptions;

namespace Billing.Domain.Catalogs;

/// <summary>
/// SUNAT Catálogo 06 — Documento de identidad.
/// </summary>
public readonly record struct IdentityDocumentType
{
    public string Code { get; }
    public string Name { get; }

    private IdentityDocumentType(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static readonly IdentityDocumentType TaxDocNoRuc = new("0", "Doc. trib. no. dom. sin RUC");
    public static readonly IdentityDocumentType Dni = new("1", "DNI");
    public static readonly IdentityDocumentType ForeignCard = new("4", "Carné de extranjería");
    public static readonly IdentityDocumentType Ruc = new("6", "RUC");
    public static readonly IdentityDocumentType Passport = new("7", "Pasaporte");
    public static readonly IdentityDocumentType DiplomaticId = new("A", "Cédula diplomática de identidad");
    public static readonly IdentityDocumentType TaxIdentificationNumber = new("B", "Doc. identidad país residencia-no.d");
    public static readonly IdentityDocumentType TaxIdNaturalPerson = new("C", "Tax Identification Number - TIN");
    public static readonly IdentityDocumentType IdentificationNumber = new("D", "Identification Number - IN");
    public static readonly IdentityDocumentType AndeanMigrationCard = new("E", "TAM - Tarjeta Andina de Migración");
    public static readonly IdentityDocumentType TemporaryPermit = new("F", "PTP - Permiso Temporal de Permanencia");
    public static readonly IdentityDocumentType SafeConduct = new("G", "Salvoconducto");

    public static IReadOnlyList<IdentityDocumentType> All { get; } =
    [
        TaxDocNoRuc, Dni, ForeignCard, Ruc, Passport, DiplomaticId,
        TaxIdentificationNumber, TaxIdNaturalPerson, IdentificationNumber,
        AndeanMigrationCard, TemporaryPermit, SafeConduct
    ];

    public bool IsRuc => this == Ruc;
    public bool IsDni => this == Dni;

    public static IdentityDocumentType FromCode(string code)
    {
        foreach (var type in All)
        {
            if (string.Equals(type.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        throw new BusinessRuleException("IDENTITY_TYPE", $"Unknown SUNAT identity document type '{code}'.");
    }

    public override string ToString() => Code;
}
