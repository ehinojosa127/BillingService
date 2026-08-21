using Billing.Application.Abstractions;
using Billing.Application.Pdf;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Pdf;

public sealed class DocumentPdfGenerator(
    IPdfTemplateComponentResolver templateResolver,
    BlazorPdfHtmlRenderer htmlRenderer,
    ChromiumHtmlToPdfRenderer pdfRenderer,
    IOptions<PdfBrandingOptions> brandingOptions,
    ILogger<DocumentPdfGenerator> logger) : IPdfGenerator
{
    public async Task<byte[]> GenerateAsync(
        ElectronicDocument document,
        byte[] qrPng,
        PdfTemplateType template,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RenderAsync(document, qrPng, template, cancellationToken);
        }
        catch (Exception ex) when (template == PdfTemplateType.Custom)
        {
            logger.LogWarning(ex, "CUSTOM PDF template failed for {Document}; falling back to DEFAULT. XML/SUNAT were not affected.", document.FullNumber);
            return await RenderAsync(document, qrPng, PdfTemplateType.Default, cancellationToken);
        }
    }

    public async Task<byte[]> GenerateFromViewModelAsync(
        BillingDocumentPdfViewModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var componentType = templateResolver.Resolve(model.TemplateType);
        var body = await htmlRenderer.RenderAsync(componentType, model);
        return await pdfRenderer.RenderAsync(WrapHtml(model.Document.FullNumber, body), cancellationToken);
    }

    private async Task<byte[]> RenderAsync(
        ElectronicDocument document,
        byte[] qrPng,
        PdfTemplateType template,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = BillingDocumentPdfMapper.Map(document, qrPng, template, brandingOptions.Value);
        var componentType = templateResolver.Resolve(template);
        var body = await htmlRenderer.RenderAsync(componentType, model);
        var html = WrapHtml(model.Document.FullNumber, body);
        if (string.IsNullOrWhiteSpace(body) || !body.Contains(model.Document.FullNumber, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PDF HTML template did not contain document data.");
        }

        return await pdfRenderer.RenderAsync(html, cancellationToken);
    }

    private static string WrapHtml(string title, string body) =>
        $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=794" />
              <title>{{System.Net.WebUtility.HtmlEncode(title)}}</title>
              <style>
                html, body { margin: 0; padding: 0; width: 100%; }
              </style>
            </head>
            <body>
              {{body}}
            </body>
            </html>
            """;
}
