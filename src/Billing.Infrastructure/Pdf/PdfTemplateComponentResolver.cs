using Billing.Application.Pdf;
using Billing.Domain.Enums;

namespace Billing.Infrastructure.Pdf;

public interface IPdfTemplateComponentResolver
{
    Type Resolve(PdfTemplateType template);
}

public sealed class PdfTemplateComponentResolver : IPdfTemplateComponentResolver
{
    public Type Resolve(PdfTemplateType template) => template switch
    {
        PdfTemplateType.Custom => typeof(Templates.Custom.CustomDocumentPdf),
        _ => typeof(Templates.Default.DefaultDocumentPdf)
    };
}
