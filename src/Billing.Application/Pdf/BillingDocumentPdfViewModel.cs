using Billing.Domain.Enums;

namespace Billing.Application.Pdf;

public sealed record BillingDocumentPdfViewModel(
    PdfTemplateType TemplateType,
    PdfIssuerViewModel Issuer,
    PdfRecipientViewModel Recipient,
    PdfDocumentInfoViewModel Document,
    IReadOnlyList<PdfItemViewModel> Items,
    PdfTaxesViewModel Taxes,
    PdfTotalsViewModel Totals,
    PdfQrViewModel Qr,
    PdfBrandingViewModel Branding,
    IReadOnlyList<PdfRelatedDocumentViewModel> RelatedDocuments,
    string? Observation)
{
    public bool IsCustom => TemplateType.IsCustom();

    public bool ShowQr { get; init; } = true;

    public bool ShowTaxBreakdown { get; init; } = true;
}

public sealed record PdfIssuerViewModel(
    string Ruc,
    string LegalName,
    string TradeName,
    string AddressLine,
    string? Email,
    string? Phone);

public sealed record PdfRecipientViewModel(
    string Name,
    string IdentityType,
    string IdentityTypeLabel,
    string IdentityNumber,
    string? Address);

public sealed record PdfDocumentInfoViewModel(
    string TypeCode,
    string TypeName,
    string TypeLabel,
    string Series,
    int Number,
    string FullNumber,
    string IssueDate,
    string? DueDate,
    string PaymentFormLabel,
    string? ExternalReference,
    string Currency,
    string? DigestValue,
    string SunatStatus);

public sealed record PdfItemViewModel(
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitLabel,
    decimal UnitValue,
    decimal UnitPrice,
    decimal IgvAmount,
    decimal Total);

public sealed record PdfTaxesViewModel(
    decimal TaxableAmount,
    decimal ExemptAmount,
    decimal UnaffectedAmount,
    decimal IgvAmount);

public sealed record PdfTotalsViewModel(
    decimal PayableAmount,
    string AmountInWords,
    string Currency);

public sealed record PdfQrViewModel(string PngDataUri);

public sealed record PdfBrandingViewModel(
    string CompanyName,
    string PrimaryColor,
    string HeaderColor,
    string? LogoDataUri,
    string FooterText);

public sealed record PdfRelatedDocumentViewModel(
    string TypeLabel,
    string FullNumber,
    string Reason);
