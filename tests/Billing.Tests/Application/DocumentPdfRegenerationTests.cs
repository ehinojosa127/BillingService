using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Application.Pdf;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Tests.Domain;
using NSubstitute;
using System.Text;

namespace Billing.Tests.Application;

file sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 8, 18, 15, 0, 0, TimeSpan.Zero);
    public DateTimeOffset LimaNow { get; } = new(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5));
}

public sealed class DocumentPdfRegenerationTests
{
    [Fact]
    public async Task SaveAsync_persists_requested_template_on_generated_file()
    {
        var document = DocumentFactory.Invoice();
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest", DateTimeOffset.UtcNow);

        var storage = CreateStorage();
        var pdf = CreatePdfGenerator();
        var store = new DocumentPdfStore(pdf, CreateQr(), storage, new FixedClock());

        await store.SaveAsync(document, PdfTemplateType.Custom, CancellationToken.None);

        var file = document.GetFile(GeneratedFileKind.Pdf);
        Assert.NotNull(file);
        Assert.Equal(PdfTemplateType.Custom, file!.GetPdfTemplateType());
        Assert.Contains(".CUSTOM.pdf", file.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegenerateAsync_replaces_template_metadata_without_touching_xml_or_cdr()
    {
        var document = DocumentFactory.Invoice();
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest", DateTimeOffset.UtcNow);
        var sunatBefore = document.SunatStatus;

        var xmlBytes = "<signed-xml />"u8.ToArray();
        var cdrBytes = "<cdr />"u8.ToArray();
        document.AddFile(GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.SignedXml,
            "xml-key",
            "doc.xml",
            "application/xml",
            DateTimeOffset.UtcNow));
        document.AddFile(GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.Cdr,
            "cdr-key",
            "doc.cdr.zip",
            "application/zip",
            DateTimeOffset.UtcNow));
        document.AddFile(GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.Pdf,
            "pdf-key",
            "doc.CUSTOM.pdf",
            "application/pdf",
            DateTimeOffset.UtcNow,
            PdfTemplateType.Custom));

        var storage = CreateStorage();
        storage.GetAsync("xml-key", Arg.Any<CancellationToken>()).Returns(new StoredFile("xml-key", "doc.xml", "application/xml", xmlBytes));
        storage.GetAsync("cdr-key", Arg.Any<CancellationToken>()).Returns(new StoredFile("cdr-key", "doc.cdr.zip", "application/zip", cdrBytes));

        var pdf = CreatePdfGenerator();
        var store = new DocumentPdfStore(pdf, CreateQr(), storage, new FixedClock());
        var handler = CreateRegenerateHandler(document, storage, store);

        var result = await handler.Handle(
            new RegenerateDocumentPdfCommand(document.Id, PdfTemplateType.Default.ToCode()),
            CancellationToken.None);

        Assert.Equal(PdfTemplateType.Default.ToCode(), result.TemplateType);
        Assert.Equal(PdfTemplateType.Default, document.GetFile(GeneratedFileKind.Pdf)!.GetPdfTemplateType());
        Assert.Contains(".DEFAULT.pdf", document.GetFile(GeneratedFileKind.Pdf)!.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("F001", document.Series);
        Assert.Equal(1, document.Number);
        Assert.Equal(SunatStatus.NotSent, document.SunatStatus);
        Assert.Equal(sunatBefore, document.SunatStatus);
    }

    [Fact]
    public async Task RegenerateAsync_failure_keeps_previous_pdf_metadata()
    {
        var document = DocumentFactory.Invoice();
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest", DateTimeOffset.UtcNow);

        var previous = GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.Pdf,
            "pdf-key",
            "doc.CUSTOM.pdf",
            "application/pdf",
            DateTimeOffset.UtcNow,
            PdfTemplateType.Custom);
        document.AddFile(previous);

        var storage = CreateStorage();
        var pdf = Substitute.For<IPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), PdfTemplateType.Default, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]>(new InvalidOperationException("render failed")));

        var store = new DocumentPdfStore(pdf, CreateQr(), storage, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.RegenerateAsync(document, PdfTemplateType.Default, CancellationToken.None));

        var current = document.GetFile(GeneratedFileKind.Pdf);
        Assert.NotNull(current);
        Assert.Equal(previous.Id, current!.Id);
        Assert.Equal(PdfTemplateType.Custom, current.GetPdfTemplateType());
    }

    [Fact]
    public void Stored_template_remains_custom_when_resolver_would_return_default()
    {
        var document = DocumentFactory.Invoice();
        document.AddFile(GeneratedFile.Create(
            document.Id,
            GeneratedFileKind.Pdf,
            "pdf-key",
            "doc.CUSTOM.pdf",
            "application/pdf",
            DateTimeOffset.UtcNow,
            PdfTemplateType.Custom));

        var store = new DocumentPdfStore(
            Substitute.For<IPdfGenerator>(),
            CreateQr(),
            CreateStorage(),
            new FixedClock());

        Assert.Equal(PdfTemplateType.Custom, store.GetStoredTemplate(document));
        Assert.Equal(PdfTemplateType.Default, new PdfTemplateResolver().Resolve(null));
    }

    private static RegenerateDocumentPdfHandler CreateRegenerateHandler(
        ElectronicDocument document,
        IFileStorage storage,
        DocumentPdfStore store)
    {
        var documents = Substitute.For<IDocumentRepository>();
        documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);

        return new RegenerateDocumentPdfHandler(
            documents,
            new PdfTemplateResolver(),
            store,
            storage,
            Substitute.For<IUnitOfWork>());
    }

    private static IPdfGenerator CreatePdfGenerator()
    {
        var pdf = Substitute.For<IPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<PdfTemplateType>(), Arg.Any<CancellationToken>())
            .Returns(ValidPdf());
        return pdf;
    }

    private static IQrCodeGenerator CreateQr()
    {
        var qr = Substitute.For<IQrCodeGenerator>();
        qr.BuildPayload(Arg.Any<ElectronicDocument>()).Returns("qr");
        qr.GeneratePng(Arg.Any<string>()).Returns([1, 2, 3]);
        return qr;
    }

    private static IFileStorage CreateStorage()
    {
        var storage = Substitute.For<IFileStorage>();
        storage.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => new StoredFile(
                call.ArgAt<string>(0),
                call.ArgAt<string>(1),
                call.ArgAt<string>(2),
                call.ArgAt<byte[]>(3)));
        return storage;
    }

    private static byte[] ValidPdf()
    {
        var builder = new StringBuilder("%PDF-1.4\n");
        builder.Append('0', 120);
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
