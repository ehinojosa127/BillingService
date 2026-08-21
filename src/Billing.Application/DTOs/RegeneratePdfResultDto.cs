namespace Billing.Application.DTOs;

public sealed record RegeneratePdfResultDto(
    Guid DocumentId,
    string TemplateType,
    DateTimeOffset GeneratedAt,
    bool PdfAvailable,
    string? FileName);
