using Billing.Application.Pdf;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Infrastructure.Pdf;

public static class BillingDocumentPdfMapper
{
    public static BillingDocumentPdfViewModel Map(
        ElectronicDocument document,
        byte[] qrPng,
        PdfTemplateType template,
        PdfBrandingOptions branding)
    {
        var custom = template.IsCustom();
        var headerColor = custom ? branding.PrimaryColor : "#475569";
        var logo = custom ? branding.LogoDataUri : null;
        var title = custom
            ? branding.CompanyName
            : (string.IsNullOrWhiteSpace(document.IssuerTradeName) ? document.IssuerLegalName : document.IssuerTradeName);
        var footer = "Representación impresa del comprobante de pago electrónico, autorizado mediante Resolución de Intendencia SUNAT. Puede verificar su validez escaneando el código QR.";

        return new BillingDocumentPdfViewModel(
            template,
            new PdfIssuerViewModel(
                document.IssuerRuc,
                document.IssuerLegalName,
                string.IsNullOrWhiteSpace(document.IssuerTradeName) ? document.IssuerLegalName : document.IssuerTradeName,
                document.IssuerAddressLine,
                document.IssuerEmail,
                document.IssuerPhone),
            new PdfRecipientViewModel(
                document.RecipientName,
                document.RecipientIdentityType,
                IdentityLabel(document.RecipientIdentityType),
                document.RecipientIdentityNumber,
                document.RecipientAddressLine),
            new PdfDocumentInfoViewModel(
                document.DocumentTypeCode,
                document.Type.Name,
                DocumentTypeLabel(document.Type),
                document.Series,
                document.Number,
                document.FullNumber,
                document.IssueDate.ToString("dd/MM/yyyy"),
                document.DueDate?.ToString("dd/MM/yyyy"),
                PaymentFormLabel(document.PaymentForm),
                document.ExternalReference,
                document.Currency,
                document.DigestValue,
                document.SunatStatus.ToString()),
            document.Items.OrderBy(x => x.LineNumber).Select(item => new PdfItemViewModel(
                item.LineNumber,
                item.Description,
                item.Quantity,
                UnitLabel(item.UnitCode),
                item.UnitValue,
                item.UnitPrice,
                item.IgvAmount,
                item.Total)).ToArray(),
            new PdfTaxesViewModel(
                document.TaxableAmount,
                document.ExemptAmount,
                document.UnaffectedAmount,
                document.IgvAmount),
            new PdfTotalsViewModel(document.PayableAmount, document.AmountInWords, document.Currency),
            new PdfQrViewModel("data:image/png;base64," + Convert.ToBase64String(qrPng)),
            new PdfBrandingViewModel(title, branding.PrimaryColor, headerColor, logo, footer),
            document.References.Select(reference => new PdfRelatedDocumentViewModel(
                DocumentTypeLabel(reference.RelatedDocumentType),
                reference.FullNumber,
                reference.ReasonDescription)).ToArray(),
            document.Observation)
        {
            ShowQr = true,
            ShowTaxBreakdown = true
        };
    }

    private static string IdentityLabel(string code) => code switch
    {
        "1" => "DNI",
        "4" => "C.E.",
        "6" => "RUC",
        "7" => "Pasaporte",
        "0" => "Doc.",
        _ => code
    };

    private static string PaymentFormLabel(PaymentForm form) =>
        form == PaymentForm.Credit ? "CRÉDITO" : "CONTADO";

    private static string UnitLabel(string code) => code switch
    {
        "NIU" => "UNIDAD",
        "ZZ" => "SERVICIO",
        _ => code
    };

    private static string DocumentTypeLabel(DocumentType type)
    {
        if (type == DocumentType.Invoice)
        {
            return "FACTURA ELECTRÓNICA";
        }

        if (type == DocumentType.Receipt)
        {
            return "BOLETA DE VENTA ELECTRÓNICA";
        }

        if (type == DocumentType.CreditNote)
        {
            return "NOTA DE CRÉDITO ELECTRÓNICA";
        }

        if (type == DocumentType.DebitNote)
        {
            return "NOTA DE DÉBITO ELECTRÓNICA";
        }

        if (type == DocumentType.ShippingGuide)
        {
            return "GUÍA DE REMISIÓN ELECTRÓNICA";
        }

        return type.Name.ToUpperInvariant() + " ELECTRÓNICA";
    }
}
