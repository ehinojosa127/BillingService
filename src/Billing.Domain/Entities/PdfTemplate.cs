using Billing.Domain.Exceptions;

namespace Billing.Domain.Entities;

/// <summary>
/// Plantilla visual del PDF. No altera XML, firma ni payload SUNAT.
/// </summary>
public sealed class PdfTemplate
{
    public const string DefaultCode = "DEFAULT";
    public const string CustomCode = "CUSTOM";

    public Guid Id { get; private set; }
    public string Code { get; private set; } = DefaultCode;
    public string Name { get; private set; } = "Default";
    public string? TradeName { get; private set; }
    public bool IsDefault { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? FooterText { get; private set; }
    public string? CommercialText { get; private set; }
    public string? LogoStorageKey { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PdfTemplate()
    {
    }

    public static PdfTemplate Create(string code, string name, bool isDefault, DateTimeOffset now)
    {
        var normalized = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("PDF_TEMPLATE", "Template name is required.");
        }

        return new PdfTemplate
        {
            Id = Guid.CreateVersion7(),
            Code = normalized,
            Name = name.Trim(),
            IsDefault = isDefault,
            UpdatedAt = now
        };
    }

    public void Update(string name, string? tradeName, string? primaryColor, string? footerText, string? commercialText, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("PDF_TEMPLATE", "Template name is required.");
        }

        Name = name.Trim();
        TradeName = TrimToNull(tradeName);
        PrimaryColor = NormalizeColor(primaryColor);
        FooterText = TrimToNull(footerText);
        CommercialText = TrimToNull(commercialText);
        UpdatedAt = now;
    }

    public void SetLogo(string? storageKey, DateTimeOffset now)
    {
        LogoStorageKey = TrimToNull(storageKey);
        UpdatedAt = now;
    }

    public void MarkDefault(DateTimeOffset now)
    {
        IsDefault = true;
        UpdatedAt = now;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    public static string NormalizeCode(string code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (value is not DefaultCode and not CustomCode)
        {
            throw new BusinessRuleException("PDF_TEMPLATE", "Supported templates are DEFAULT and CUSTOM.");
        }

        return value;
    }

    private static string? NormalizeColor(string? color)
    {
        var value = TrimToNull(color);
        if (value is null)
        {
            return null;
        }

        if (!value.StartsWith('#'))
        {
            value = "#" + value;
        }

        if (value.Length is not 7 || value[1..].Any(c => !Uri.IsHexDigit(c)))
        {
            throw new BusinessRuleException("PDF_TEMPLATE", "Primary color must be a hex value like #1F4E79.");
        }

        return value.ToUpperInvariant();
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
