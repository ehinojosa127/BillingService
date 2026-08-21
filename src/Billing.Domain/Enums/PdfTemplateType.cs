using Billing.Domain.Exceptions;

namespace Billing.Domain.Enums;

public enum PdfTemplateType
{
    Default = 0,
    Custom = 1
}

public static class PdfTemplateTypeExtensions
{
    public static string ToCode(this PdfTemplateType template) => template switch
    {
        PdfTemplateType.Custom => "CUSTOM",
        _ => "DEFAULT"
    };

    public static bool IsCustom(this PdfTemplateType template) => template == PdfTemplateType.Custom;

    public static PdfTemplateType Parse(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant().Replace('-', '_');
        return normalized switch
        {
            "" or "DEFAULT" or "SUNAT_DEFAULT" or "SUNAT" => PdfTemplateType.Default,
            "CUSTOM" or "COMPANY" => PdfTemplateType.Custom,
            _ => throw new BusinessRuleException("PDF_TEMPLATE", "Supported templates are DEFAULT and CUSTOM.")
        };
    }

    public static bool TryParse(string? value, out PdfTemplateType template)
    {
        try
        {
            template = Parse(value);
            return true;
        }
        catch (BusinessRuleException)
        {
            template = PdfTemplateType.Default;
            return false;
        }
    }
}
