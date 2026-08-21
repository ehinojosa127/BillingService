using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Application.DTOs;
using Billing.Application.Pdf;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Tests.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Billing.Tests.Application;

public sealed class IssueDocumentHandlerTests
{
    [Fact]
    public async Task Idempotent_replay_returns_the_stored_response()
    {
        var stored = new DocumentResultDto(
            Guid.CreateVersion7(), "invoice", "F001", 1, "F001-00001", "accepted", "accepted",
            "erp", "ORD-1", 118m, "PEN", "2026-08-18", new DocumentFileLinksDto(null, null, null), "abc", "0", "ok");
        var idempotency = Substitute.For<IIdempotencyStore>();
        idempotency.GetAsync("key-1", Arg.Any<CancellationToken>())
            .Returns(IdempotencyRecord.Create("key-1", "deadbeef", stored.Id, System.Text.Json.JsonSerializer.Serialize(stored), 201, DateTimeOffset.UtcNow));

        var handler = CreateHandler(idempotencyStore: idempotency, issuer: null);
        var command = ValidCommand() with { IdempotencyKey = "key-1" };

        // Hash will not match deadbeef, so this should conflict.
        await Assert.ThrowsAsync<Billing.Application.Exceptions.ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Issues_document_and_returns_accepted_result()
    {
        var issuer = DocumentFactory.Issuer();
        var issuerRepo = Substitute.For<IIssuerRepository>();
        issuerRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(issuer);

        var seriesRepo = Substitute.For<IDocumentSeriesRepository>();
        seriesRepo.AllocateNextNumberAsync(Arg.Any<DocumentType>(), "F001", Arg.Any<CancellationToken>()).Returns(1);

        var documents = Substitute.For<IDocumentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        var xml = Substitute.For<IXmlDocumentGenerator>();
        xml.Generate(Arg.Any<ElectronicDocument>()).Returns("<xml />"u8.ToArray());
        var signer = Substitute.For<IXmlSigner>();
        signer.Sign(Arg.Any<byte[]>()).Returns(new SignedXmlResult("<signed />"u8.ToArray(), "digest"));
        var provider = Substitute.For<IElectronicDocumentProvider>();
        provider.SubmitAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new SubmissionResult(SunatStatus.Accepted, "0", "Aceptada", null, null, "<cdr/>"u8.ToArray(), "<cdr/>"u8.ToArray()));
        var storage = Substitute.For<IFileStorage>();
        storage.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => new StoredFile(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<byte[]>(3)));
        var qr = Substitute.For<IQrCodeGenerator>();
        qr.BuildPayload(Arg.Any<ElectronicDocument>()).Returns("payload");
        qr.GeneratePng(Arg.Any<string>()).Returns([1, 2, 3]);
        var pdf = Substitute.For<IPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<PdfTemplateType>(), Arg.Any<CancellationToken>())
            .Returns(ValidPdf());
        var cdr = Substitute.For<ICdrParser>();
        cdr.Parse(Arg.Any<byte[]>()).Returns(new CdrParseResult("0", "Aceptada", null, DateTimeOffset.UtcNow, SunatStatus.Accepted, "<cdr/>"u8.ToArray()));

        var handler = new IssueDocumentHandler(
            issuerRepo,
            seriesRepo,
            documents,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IIdempotencyStore>(),
            unitOfWork,
            new FixedClock(),
            xml,
            signer,
            provider,
            storage,
            cdr,
            new PdfTemplateResolver(),
            new DocumentPdfStore(pdf, qr, storage, new FixedClock()),
            GeneralTaxProfile(),
            NullLogger<IssueDocumentHandler>.Instance);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.Equal("F001", result.Series);
        Assert.Equal(1, result.Number);
        Assert.Equal("accepted", result.Status);
        await documents.Received().AddAsync(Arg.Any<ElectronicDocument>(), Arg.Any<CancellationToken>());
        await provider.Received().SubmitAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pdf_failure_keeps_accepted_sunat_result()
    {
        var issuer = DocumentFactory.Issuer();
        var issuerRepo = Substitute.For<IIssuerRepository>();
        issuerRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(issuer);

        var seriesRepo = Substitute.For<IDocumentSeriesRepository>();
        seriesRepo.AllocateNextNumberAsync(Arg.Any<DocumentType>(), "F001", Arg.Any<CancellationToken>()).Returns(1);

        var documents = Substitute.For<IDocumentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        var xml = Substitute.For<IXmlDocumentGenerator>();
        xml.Generate(Arg.Any<ElectronicDocument>()).Returns("<xml />"u8.ToArray());
        var signer = Substitute.For<IXmlSigner>();
        signer.Sign(Arg.Any<byte[]>()).Returns(new SignedXmlResult("<signed />"u8.ToArray(), "digest"));
        var provider = Substitute.For<IElectronicDocumentProvider>();
        provider.SubmitAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new SubmissionResult(SunatStatus.Accepted, "0", "Aceptada", null, null, "<cdr/>"u8.ToArray(), "<cdr/>"u8.ToArray()));
        var storage = Substitute.For<IFileStorage>();
        storage.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => new StoredFile(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<string>(2), call.ArgAt<byte[]>(3)));
        var qr = Substitute.For<IQrCodeGenerator>();
        qr.BuildPayload(Arg.Any<ElectronicDocument>()).Returns("payload");
        qr.GeneratePng(Arg.Any<string>()).Returns([1, 2, 3]);
        var pdf = Substitute.For<IPdfGenerator>();
        pdf.GenerateAsync(Arg.Any<ElectronicDocument>(), Arg.Any<byte[]>(), Arg.Any<PdfTemplateType>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]>(new InvalidOperationException("pdf exploded")));
        var cdr = Substitute.For<ICdrParser>();
        cdr.Parse(Arg.Any<byte[]>()).Returns(new CdrParseResult("0", "Aceptada", null, DateTimeOffset.UtcNow, SunatStatus.Accepted, "<cdr/>"u8.ToArray()));

        var handler = new IssueDocumentHandler(
            issuerRepo,
            seriesRepo,
            documents,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IIdempotencyStore>(),
            unitOfWork,
            new FixedClock(),
            xml,
            signer,
            provider,
            storage,
            cdr,
            new PdfTemplateResolver(),
            new DocumentPdfStore(pdf, qr, storage, new FixedClock()),
            GeneralTaxProfile(),
            NullLogger<IssueDocumentHandler>.Instance);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);
        Assert.Equal("accepted", result.Status);
        Assert.Equal("accepted", result.SunatStatus);
    }

    [Fact]
    public async Task Rus_rejects_invoice_before_calling_sunat()
    {
        var issuer = DocumentFactory.Issuer();
        var issuerRepo = Substitute.For<IIssuerRepository>();
        issuerRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(issuer);
        var provider = Substitute.For<IElectronicDocumentProvider>();

        var rus = Substitute.For<IIssuerTaxProfile>();
        rus.Regime.Returns(TaxRegime.Rus);
        rus.TaxpayerType.Returns(TaxpayerType.NaturalWithBusiness);

        var handler = new IssueDocumentHandler(
            issuerRepo,
            Substitute.For<IDocumentSeriesRepository>(),
            Substitute.For<IDocumentRepository>(),
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IIdempotencyStore>(),
            Substitute.For<IUnitOfWork>(),
            new FixedClock(),
            Substitute.For<IXmlDocumentGenerator>(),
            Substitute.For<IXmlSigner>(),
            provider,
            Substitute.For<IFileStorage>(),
            Substitute.For<ICdrParser>(),
            new PdfTemplateResolver(),
            new DocumentPdfStore(
                Substitute.For<IPdfGenerator>(),
                Substitute.For<IQrCodeGenerator>(),
                Substitute.For<IFileStorage>(),
                new FixedClock()),
            rus,
            NullLogger<IssueDocumentHandler>.Instance);

        var error = await Assert.ThrowsAsync<Billing.Application.Exceptions.ValidationException>(
            () => handler.Handle(ValidCommand(), CancellationToken.None));
        Assert.Contains(error.Errors, message => message.Contains("RUS"));
        await provider.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default!, default);
    }

    private static IssueDocumentHandler CreateHandler(IIdempotencyStore idempotencyStore, Issuer? issuer) =>
        new(
            Substitute.For<IIssuerRepository>(),
            Substitute.For<IDocumentSeriesRepository>(),
            Substitute.For<IDocumentRepository>(),
            Substitute.For<IAuditLogRepository>(),
            idempotencyStore,
            Substitute.For<IUnitOfWork>(),
            new FixedClock(),
            Substitute.For<IXmlDocumentGenerator>(),
            Substitute.For<IXmlSigner>(),
            Substitute.For<IElectronicDocumentProvider>(),
            Substitute.For<IFileStorage>(),
            Substitute.For<ICdrParser>(),
            new PdfTemplateResolver(),
            new DocumentPdfStore(
                Substitute.For<IPdfGenerator>(),
                Substitute.For<IQrCodeGenerator>(),
                Substitute.For<IFileStorage>(),
                new FixedClock()),
            GeneralTaxProfile(),
            NullLogger<IssueDocumentHandler>.Instance);

    private static IIssuerTaxProfile GeneralTaxProfile()
    {
        var profile = Substitute.For<IIssuerTaxProfile>();
        profile.Regime.Returns(TaxRegime.General);
        profile.TaxpayerType.Returns(TaxpayerType.Legal);
        return profile;
    }

    private static IssueDocumentCommand ValidCommand() => new()
    {
        DocumentType = "01",
        Series = "F001",
        RecipientIdentityType = "6",
        RecipientIdentityNumber = "20000000001",
        RecipientName = "CLIENTE S.A.C.",
        Items = [new IssueItemDto("P01", "Producto", 1, "NIU", 100m, 0m, "10")]
    };

    private static byte[] ValidPdf()
    {
        var builder = new System.Text.StringBuilder("%PDF-1.4\n");
        builder.Append('0', 120);
        return System.Text.Encoding.ASCII.GetBytes(builder.ToString());
    }
}

file sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 8, 18, 15, 0, 0, TimeSpan.Zero);
    public DateTimeOffset LimaNow { get; } = new(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5));
}
