using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class GeneratedFile
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public GeneratedFileKind Kind { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string? PdfTemplate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private GeneratedFile()
    {
    }

    public static GeneratedFile Create(
        Guid documentId,
        GeneratedFileKind kind,
        string storageKey,
        string fileName,
        string contentType,
        DateTimeOffset now,
        PdfTemplateType? pdfTemplate = null)
    {
        return new GeneratedFile
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            Kind = kind,
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = contentType,
            PdfTemplate = kind == GeneratedFileKind.Pdf ? pdfTemplate?.ToCode() : null,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public PdfTemplateType? GetPdfTemplateType() =>
        PdfTemplateTypeExtensions.TryParse(PdfTemplate, out var template) ? template : null;
}
