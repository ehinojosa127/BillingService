using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application.Pdf;

public sealed record PdfRegenerationResult(
    byte[] Content,
    PdfTemplateType TemplateType,
    string FileName,
    DateTimeOffset GeneratedAt);

public sealed class DocumentPdfStore(
    IPdfGenerator pdfGenerator,
    IQrCodeGenerator qrCodeGenerator,
    IFileStorage fileStorage,
    IClock clock)
{
    public PdfTemplateType? GetStoredTemplate(ElectronicDocument document) =>
        document.GetFile(GeneratedFileKind.Pdf)?.GetPdfTemplateType();

    public async Task<byte[]> SaveAsync(
        ElectronicDocument document,
        PdfTemplateType template,
        CancellationToken cancellationToken)
    {
        var result = await GenerateAndPersistAsync(document, template, cancellationToken);
        return result.Content;
    }

    public async Task<PdfRegenerationResult> RegenerateAsync(
        ElectronicDocument document,
        PdfTemplateType template,
        CancellationToken cancellationToken)
    {
        return await GenerateAndPersistAsync(document, template, cancellationToken);
    }

    private async Task<PdfRegenerationResult> GenerateAndPersistAsync(
        ElectronicDocument document,
        PdfTemplateType template,
        CancellationToken cancellationToken)
    {
        var qr = qrCodeGenerator.GeneratePng(qrCodeGenerator.BuildPayload(document));
        var pdf = await pdfGenerator.GenerateAsync(document, qr, template, cancellationToken);

        if (pdf.Length < 100 || !System.Text.Encoding.ASCII.GetString(pdf, 0, 4).StartsWith("%PDF", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated PDF is invalid.");
        }

        var fileName = $"{document.XmlFileName}.{template.ToCode()}.pdf";
        var key = BuildStorageKey(document);
        var saved = await fileStorage.SaveAsync(key, fileName, "application/pdf", pdf, cancellationToken);
        var generatedAt = clock.UtcNow;
        document.AddFile(GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.Pdf,
            saved.Key,
            saved.FileName,
            saved.ContentType,
            generatedAt,
            template));

        return new PdfRegenerationResult(pdf, template, saved.FileName, generatedAt);
    }

    internal static string BuildStorageKey(ElectronicDocument document) =>
        $"{document.IssuerRuc}/{document.DocumentTypeCode}/{document.Series}/{document.Number}/{GeneratedFileKind.Pdf}-{document.XmlFileName}.pdf";
}
