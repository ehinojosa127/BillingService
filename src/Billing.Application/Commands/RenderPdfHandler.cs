using Billing.Application.Abstractions;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
using Billing.Domain.Services;
using MediatR;

namespace Billing.Application.Commands;

public sealed class RenderPdfHandler(
    IIssuerRepository issuers,
    IPdfGenerator pdfGenerator,
    IPdfTemplateResolver pdfTemplateResolver,
    IPdfBrandingProvider brandingProvider) : IRequestHandler<RenderPdfCommand, byte[]>
{
    public async Task<byte[]> Handle(RenderPdfCommand request, CancellationToken cancellationToken)
    {
        var issuer = await issuers.GetAsync(cancellationToken)
                     ?? throw new NotFoundException("Issuer has not been configured.");

        var template = pdfTemplateResolver.Resolve(request.PdfTemplate);
        var branding = await brandingProvider.GetAsync(cancellationToken);
        var logoDataUri = branding?.Logo is { Length: > 0 }
            ? "data:image/png;base64," + Convert.ToBase64String(branding.Logo)
            : null;
        var headerColor = string.IsNullOrWhiteSpace(branding?.PrimaryColor) ? "#1F4E79" : branding.PrimaryColor;
        var companyName = string.IsNullOrWhiteSpace(issuer.TradeName) ? issuer.LegalName : issuer.TradeName;
        var footer = string.IsNullOrWhiteSpace(request.FooterText)
            ? "Documento interno. No constituye comprobante de pago electrónico ni tiene validez tributaria ante SUNAT."
            : request.FooterText;

        var items = request.Items.Select((item, index) => new PdfItemViewModel(
            index + 1,
            item.Description,
            item.Quantity,
            "UNIDAD",
            item.UnitPrice,
            item.UnitPrice,
            0m,
            item.Total)).ToArray();

        var identityLabel = request.RecipientIdentityType switch
        {
            "6" => "RUC",
            "4" => "C.E.",
            "7" => "Pasaporte",
            _ => "DNI"
        };

        var model = new BillingDocumentPdfViewModel(
            template,
            new PdfIssuerViewModel(
                issuer.Ruc,
                issuer.LegalName,
                companyName,
                issuer.Address.Line,
                issuer.Email,
                issuer.Phone),
            new PdfRecipientViewModel(
                request.RecipientName,
                request.RecipientIdentityType,
                identityLabel,
                request.RecipientIdentityNumber,
                request.RecipientAddress),
            new PdfDocumentInfoViewModel(
                "00",
                request.TypeLabel,
                request.TypeLabel,
                request.Series,
                request.Number,
                request.FullNumber,
                request.IssueDate,
                null,
                "CONTADO",
                request.ExternalReference,
                "PEN",
                null,
                "Interno"),
            items,
            new PdfTaxesViewModel(request.PayableAmount, 0m, 0m, 0m),
            new PdfTotalsViewModel(
                request.PayableAmount,
                AmountInWords.ForCurrency(request.PayableAmount, "PEN"),
                "PEN"),
            new PdfQrViewModel(""),
            new PdfBrandingViewModel(companyName, headerColor, headerColor, logoDataUri, footer),
            [],
            request.Observation)
        {
            ShowQr = request.ShowQr,
            ShowTaxBreakdown = request.ShowTaxBreakdown
        };

        return await pdfGenerator.GenerateFromViewModelAsync(model, cancellationToken);
    }
}
