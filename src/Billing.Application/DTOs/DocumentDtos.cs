using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Services;

namespace Billing.Application.DTOs;

public sealed record DocumentFileLinksDto(
    string? Xml,
    string? Pdf,
    string? Cdr);

public sealed record DocumentItemResultDto(
    int LineNumber,
    string? Code,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitValue,
    decimal IgvAmount,
    decimal Total);

public sealed record DocumentSubmissionDto(
    int Attempt,
    string Status,
    string? ResponseCode,
    string? Description,
    string? Notes,
    string? Ticket,
    string? ErrorKind,
    string StartedAt,
    string? CompletedAt);

public sealed record DocumentResultDto(
    Guid Id,
    string DocumentType,
    string Series,
    int Number,
    string FullNumber,
    string Status,
    string SunatStatus,
    string? ExternalSystem,
    string? ExternalReference,
    decimal PayableAmount,
    string Currency,
    string IssueDate,
    DocumentFileLinksDto Files,
    string? DigestValue,
    string? SunatResponseCode,
    string? SunatDescription,
    string? ExternalEntity = null,
    string? ExternalId = null,
    bool CanRetry = false,
    string? RecipientName = null,
    string? RecipientIdentityType = null,
    string? RecipientIdentityNumber = null,
    string? RecipientAddress = null,
    string? IssuerLegalName = null,
    string? IssuerRuc = null,
    string? IssuerTradeName = null,
    decimal TaxableAmount = 0,
    decimal IgvAmount = 0,
    string? Observation = null,
    IReadOnlyList<DocumentItemResultDto>? Items = null,
    DocumentSubmissionDto? LastSubmission = null,
        int AttemptCount = 0,
    bool CanCancel = false,
    bool CanConsult = false);

public sealed record DocumentListItemDto(
    Guid Id,
    string DocumentType,
    string Series,
    int Number,
    string FullNumber,
    string Status,
    string SunatStatus,
    string IssueDate,
    decimal PayableAmount,
    string? ExternalReference,
    string? ExternalSystem = null,
    string? ExternalEntity = null,
    string? ExternalId = null,
    string? RecipientName = null,
    string? RecipientIdentityNumber = null,
    bool CanRetry = false,
    bool CanCancel = false,
    bool CanConsult = false);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);

public sealed record DocumentStatusDto(
    Guid Id,
    string Status,
    string SunatStatus,
    string? ResponseCode,
    string? Description,
    string? Ticket,
    bool CanRetry = false,
    string? Notes = null,
    int AttemptCount = 0,
    string? LastAttemptAt = null,
    bool CanCancel = false,
    bool CanConsult = false);

public sealed record IssuerDto(
    Guid Id,
    string Ruc,
    string LegalName,
    string TradeName,
    string AddressLine,
    string Ubigeo,
    string Department,
    string Province,
    string District,
    string CountryCode,
    string? Urbanization,
    string EstablishmentCode,
    string? Email,
    string? Phone);

public sealed record IssuerCapabilitiesDto(
    string TaxRegime,
    string TaxRegimeName,
    string TaxpayerType,
    string TaxpayerTypeName,
    IReadOnlyList<string> AllowedDocumentTypes,
    bool CanIssueInvoice,
    bool CanIssueReceipt);

public sealed record SeriesDto(
    Guid Id,
    string DocumentType,
    string Series,
    int LastNumber,
    bool IsActive);

public sealed record PdfTemplateDto(
    Guid Id,
    string Code,
    string Name,
    bool IsDefault,
    string? TradeName,
    string? PrimaryColor,
    string? FooterText,
    string? CommercialText,
    bool HasLogo);

public static class DocumentMapper
{
    public static DocumentResultDto ToResult(ElectronicDocument document)
    {
        var last = document.Submissions.OrderBy(x => x.Attempt).LastOrDefault();
        return new DocumentResultDto(
            document.Id,
            ToApiDocumentType(document),
            document.Series,
            document.Number,
            document.FullNumber,
            ToApiStatus(document.Status),
            ToApiSunatStatus(document.SunatStatus),
            document.ExternalSystem,
            document.ExternalReference,
            document.PayableAmount,
            document.Currency,
            document.IssueDate.ToString("yyyy-MM-dd"),
            new DocumentFileLinksDto(
                $"/api/v1/documents/{document.Id}/xml",
                $"/api/v1/documents/{document.Id}/pdf",
                document.GetFile(GeneratedFileKind.Cdr) is null ? null : $"/api/v1/documents/{document.Id}/cdr"),
            document.DigestValue,
            last?.ResponseCode,
            last?.Description,
            document.ExternalEntity,
            document.ExternalId,
            DocumentStatusMachine.CanRetrySubmission(document),
            document.RecipientName,
            document.RecipientIdentityType,
            document.RecipientIdentityNumber,
            document.RecipientAddressLine,
            document.IssuerLegalName,
            document.IssuerRuc,
            document.IssuerTradeName,
            document.TaxableAmount,
            document.IgvAmount,
            document.Observation,
            document.Items.OrderBy(x => x.LineNumber).Select(ToItem).ToArray(),
            last is null ? null : ToSubmission(last),
            document.Submissions.Count,
            DocumentStatusMachine.CanCancel(document),
            DocumentStatusMachine.CanConsult(document));
    }

    public static DocumentListItemDto ToListItem(ElectronicDocument document) =>
        new(
            document.Id,
            ToApiDocumentType(document),
            document.Series,
            document.Number,
            document.FullNumber,
            ToApiStatus(document.Status),
            ToApiSunatStatus(document.SunatStatus),
            document.IssueDate.ToString("yyyy-MM-dd"),
            document.PayableAmount,
            document.ExternalReference,
            document.ExternalSystem,
            document.ExternalEntity,
            document.ExternalId,
            document.RecipientName,
            document.RecipientIdentityNumber,
            DocumentStatusMachine.CanRetrySubmission(document),
            DocumentStatusMachine.CanCancel(document),
            DocumentStatusMachine.CanConsult(document));

    public static DocumentItemResultDto ToItem(DocumentItem item) =>
        new(item.LineNumber, item.Code, item.Description, item.Quantity, item.UnitCode, item.UnitValue, item.IgvAmount, item.Total);

    public static DocumentSubmissionDto ToSubmission(DocumentSubmission submission) =>
        new(
            submission.Attempt,
            ToApiSunatStatus(submission.Status),
            submission.ResponseCode,
            submission.Description,
            submission.Notes,
            submission.Ticket,
            submission.ErrorKind,
            submission.StartedAt.ToString("O"),
            submission.CompletedAt?.ToString("O"));

    public static string ToApiDocumentType(ElectronicDocument document) => document.Type.Name.ToLowerInvariant() switch
    {
        "factura" => "invoice",
        "boleta" => "receipt",
        "nota de crédito" => "creditNote",
        "nota de débito" => "debitNote",
        "guía de remisión remitente" => "shippingGuide",
        _ => document.DocumentTypeCode
    };

    public static string ToApiStatus(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "draft",
        DocumentStatus.Generated => "generated",
        DocumentStatus.Signed => "signed",
        DocumentStatus.Sent => "sent",
        DocumentStatus.Accepted => "accepted",
        DocumentStatus.Observed => "observed",
        DocumentStatus.Rejected => "rejected",
        DocumentStatus.Failed => "failed",
        DocumentStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant()
    };

    public static string ToApiSunatStatus(SunatStatus status) => status switch
    {
        SunatStatus.NotSent => "notSent",
        SunatStatus.Pending => "pending",
        SunatStatus.Accepted => "accepted",
        SunatStatus.AcceptedWithObservations => "acceptedWithObservations",
        SunatStatus.Rejected => "rejected",
        SunatStatus.InProcess => "inProcess",
        SunatStatus.CommunicationError => "communicationError",
        _ => status.ToString().ToLowerInvariant()
    };

    public static PdfTemplateDto ToTemplate(PdfTemplate template) =>
        new(
            template.Id,
            template.Code,
            template.Name,
            template.IsDefault,
            template.TradeName,
            template.PrimaryColor,
            template.FooterText,
            template.CommercialText,
            !string.IsNullOrWhiteSpace(template.LogoStorageKey));
}
