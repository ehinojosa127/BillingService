using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application.Abstractions;

public sealed record StoredFile(string Key, string FileName, string ContentType, byte[] Content);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(string key, string fileName, string contentType, byte[] content, CancellationToken cancellationToken);
    Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken);
}

public interface IXmlDocumentGenerator
{
    byte[] Generate(ElectronicDocument document);
}

public sealed record SignedXmlResult(byte[] Xml, string DigestValue);

public interface IXmlSigner
{
    SignedXmlResult Sign(byte[] xml);
}

public interface ICertificateProvider
{
    System.Security.Cryptography.X509Certificates.X509Certificate2 GetCertificate();
}

public interface IQrCodeGenerator
{
    byte[] GeneratePng(string payload);
    string BuildPayload(ElectronicDocument document);
}

public interface IPdfGenerator
{
    Task<byte[]> GenerateAsync(
        ElectronicDocument document,
        byte[] qrPng,
        PdfTemplateType template,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateFromViewModelAsync(
        Billing.Application.Pdf.BillingDocumentPdfViewModel model,
        CancellationToken cancellationToken = default);
}

public interface IPdfTemplateResolver
{
    PdfTemplateType Resolve(string? requested);
}

public sealed record PdfBranding(
    string TemplateCode,
    string? TradeName,
    string? PrimaryColor,
    string? FooterText,
    string? CommercialText,
    byte[]? Logo);

public interface IPdfBrandingProvider
{
    Task<PdfBranding?> GetAsync(CancellationToken cancellationToken);
}

public sealed record CdrParseResult(
    string ResponseCode,
    string Description,
    string? Notes,
    DateTimeOffset? IssueDate,
    SunatStatus Status,
    byte[] OriginalXml);

public interface ICdrParser
{
    CdrParseResult Parse(byte[] zipOrXml);
}

public sealed record SubmissionResult(
    SunatStatus Status,
    string? ResponseCode,
    string Description,
    string? Notes,
    string? Ticket,
    byte[]? CdrZip,
    byte[]? CdrXml);

public interface IElectronicDocumentProvider
{
    Task<SubmissionResult> SubmitAsync(ElectronicDocument document, byte[] signedXml, CancellationToken cancellationToken);
    Task<SubmissionResult> GetStatusAsync(ElectronicDocument document, string? ticket, CancellationToken cancellationToken);
    Task<SubmissionResult> SendSummaryAsync(string xmlFileName, byte[] signedXml, CancellationToken cancellationToken);
    Task<SubmissionResult> GetSummaryStatusAsync(string ticket, CancellationToken cancellationToken);
}

public interface IVoidedDocumentsXmlGenerator
{
    byte[] Generate(ElectronicDocument document, string voidId, DateOnly issueDate, string reason);
}
