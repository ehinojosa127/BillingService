using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Pdf;
using Billing.Infrastructure.Qr;
using Billing.Tests.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;

namespace Billing.Tests.Infrastructure;

public sealed class QrAndPdfTests
{
    private static readonly Lazy<DocumentPdfGenerator> Generator = new(CreateGenerator);

    [Fact]
    public void Qr_payload_follows_sunat_pipe_format()
    {
        var document = DocumentFactory.Invoice();
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("abcDigest", DateTimeOffset.UtcNow);
        var payload = new SunatQrCodeGenerator().BuildPayload(document);
        Assert.Equal("20100070970|01|F001|1|18.00|118.00|2026-08-18|6|20000000001|abcDigest", payload);
        Assert.NotEmpty(new SunatQrCodeGenerator().GeneratePng(payload));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("07")]
    [InlineData("08")]
    public async Task Default_template_renders_valid_pdf(string typeCode)
    {
        await AssertPdfAsync(CreateDocument(typeCode), PdfTemplateType.Default, expectCustomBranding: false);
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("07")]
    [InlineData("08")]
    public async Task Custom_template_renders_valid_pdf_with_logo(string typeCode)
    {
        await AssertPdfAsync(CreateDocument(typeCode), PdfTemplateType.Custom, expectCustomBranding: true);
    }

    [Fact]
    public async Task Custom_pdf_is_larger_than_default_because_of_logo()
    {
        var document = DocumentFactory.Invoice();
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest-visual", DateTimeOffset.UtcNow);
        var custom = await RenderAsync(document, PdfTemplateType.Custom);
        var standard = await RenderAsync(document, PdfTemplateType.Default);
        WriteSample("custom-invoice-visual.pdf", custom);
        WriteSample("default-invoice-visual.pdf", standard);
        Assert.True(custom.Length > standard.Length + 50_000, "CUSTOM PDF should embed the brand logo.");
    }

    private static ElectronicDocument CreateDocument(string typeCode) => typeCode switch
    {
        "03" => DocumentFactory.Receipt(),
        "07" => DocumentFactory.CreditNote(),
        "08" => DocumentFactory.DebitNote(),
        _ => DocumentFactory.Invoice()
    };

    private static async Task AssertPdfAsync(
        ElectronicDocument document,
        PdfTemplateType template,
        bool expectCustomBranding)
    {
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("abcDigest", DateTimeOffset.UtcNow);
        var pdf = await RenderAsync(document, template);
        WriteSample($"{template.ToCode().ToLowerInvariant()}-{document.DocumentTypeCode}-{document.FullNumber}.pdf", pdf);
        AssertValidPdf(pdf);
        if (expectCustomBranding)
        {
            Assert.True(new PdfBrandingOptions().HasLogo);
            Assert.True(pdf.Length > 80_000, "CUSTOM PDF should embed the brand logo.");
        }
    }

    private static Task<byte[]> RenderAsync(ElectronicDocument document, PdfTemplateType template)
    {
        var qr = new SunatQrCodeGenerator().GeneratePng(new SunatQrCodeGenerator().BuildPayload(document));
        return Generator.Value.GenerateAsync(document, qr, template);
    }

    private static DocumentPdfGenerator CreateGenerator() =>
        new(
            new PdfTemplateComponentResolver(),
            new BlazorPdfHtmlRenderer(),
            new ChromiumHtmlToPdfRenderer(NullLogger<ChromiumHtmlToPdfRenderer>.Instance),
            Options.Create(new PdfBrandingOptions()),
            NullLogger<DocumentPdfGenerator>.Instance);

    private static void AssertValidPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static void WriteSample(string fileName, byte[] pdf)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "pdf-samples");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, fileName), pdf);
        var workspaceSamples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "pdf-samples"));
        Directory.CreateDirectory(workspaceSamples);
        File.WriteAllBytes(Path.Combine(workspaceSamples, fileName), pdf);
    }
}
