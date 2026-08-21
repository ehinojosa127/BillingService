using Billing.Application.Abstractions;
using Billing.Domain.Enums;

namespace Billing.Application.Pdf;

public sealed class PdfTemplateResolver : IPdfTemplateResolver
{
    public PdfTemplateType Resolve(string? requested) =>
        string.IsNullOrWhiteSpace(requested)
            ? PdfTemplateType.Default
            : PdfTemplateTypeExtensions.Parse(requested);
}
