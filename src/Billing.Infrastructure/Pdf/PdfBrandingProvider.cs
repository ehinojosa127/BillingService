using Billing.Application.Abstractions;
using Billing.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Pdf;

public sealed class PdfBrandingProvider(IOptions<PdfBrandingOptions> options) : IPdfBrandingProvider
{
    public Task<PdfBranding?> GetAsync(CancellationToken cancellationToken)
    {
        var branding = options.Value;
        return Task.FromResult<PdfBranding?>(new PdfBranding(
            PdfTemplateType.Custom.ToCode(),
            branding.CompanyName,
            branding.PrimaryColor,
            $"Representación impresa del comprobante electrónico emitido por {branding.CompanyName}.",
            null,
            branding.ResolveLogoPath() is { } path ? File.ReadAllBytes(path) : null));
    }
}
