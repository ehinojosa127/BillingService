using Billing.Application.DTOs;
using Billing.Application.Queries;
using MediatR;

namespace Billing.Application.Commands;

public sealed record IssueItemDto(
    string? Code,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitValue,
    decimal Discount,
    string TaxAffectation,
    decimal? TaxInclusiveUnitPrice = null);

public sealed record RelatedDocumentDto(
    string DocumentType,
    string Series,
    int Number,
    string ReasonCode,
    string? ReasonDescription);

public sealed record AddressDto(
    string Line,
    string Ubigeo,
    string Department,
    string Province,
    string District,
    string CountryCode,
    string? Urbanization);

public sealed record ShippingGuideDto(
    string TransferReason,
    string TransportMode,
    DateOnly TransferStartDate,
    decimal GrossWeightKg,
    int PackageCount,
    AddressDto Origin,
    AddressDto Destination,
    string? CarrierRuc,
    string? CarrierName,
    string? VehiclePlate,
    string? DriverLicense,
    string? DriverDocumentType,
    string? DriverDocumentNumber,
    string? Observation);

public sealed record IssueDocumentCommand : IRequest<DocumentResultDto>
{
    public required string DocumentType { get; init; }
    public required string Series { get; init; }
    public required string RecipientIdentityType { get; init; }
    public required string RecipientIdentityNumber { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientAddress { get; init; }
    public string? RecipientEmail { get; init; }
    public string Currency { get; init; } = "PEN";
    public string OperationType { get; init; } = "0101";
    public string PaymentForm { get; init; } = "cash";
    public DateOnly? IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? Observation { get; init; }
    public string? ExternalSystem { get; init; }
    public string? ExternalEntity { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalReference { get; init; }
    public string? RequestedBy { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? CorrelationId { get; init; }
    public decimal GlobalDiscount { get; init; }
    public decimal GlobalCharge { get; init; }
    public RelatedDocumentDto? RelatedDocument { get; init; }
    public ShippingGuideDto? ShippingGuide { get; init; }
    public required IReadOnlyList<IssueItemDto> Items { get; init; }
    public string? PdfTemplate { get; init; }
}

public sealed record RetrySubmissionCommand(Guid DocumentId, string? CorrelationId, string? RequestedBy)
    : IRequest<DocumentResultDto>;

public sealed record ConsultSunatStatusCommand(Guid DocumentId, string? CorrelationId, string? RequestedBy)
    : IRequest<DocumentResultDto>;

public sealed record CancelDocumentCommand(Guid DocumentId, string? Reason, string? CorrelationId, string? RequestedBy)
    : IRequest<DocumentResultDto>;

public sealed record UpsertIssuerCommand : IRequest<IssuerDto>
{
    public required string Ruc { get; init; }
    public required string LegalName { get; init; }
    public string? TradeName { get; init; }
    public required string AddressLine { get; init; }
    public required string Ubigeo { get; init; }
    public required string Department { get; init; }
    public required string Province { get; init; }
    public required string District { get; init; }
    public string CountryCode { get; init; } = "PE";
    public string? Urbanization { get; init; }
    public string EstablishmentCode { get; init; } = "0000";
    public string? Email { get; init; }
    public string? Phone { get; init; }
}

public sealed record CreateSeriesCommand(string DocumentType, string Series) : IRequest<SeriesDto>;

public sealed record UpsertPdfTemplateCommand : IRequest<PdfTemplateDto>
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? TradeName { get; init; }
    public string? PrimaryColor { get; init; }
    public string? FooterText { get; init; }
    public string? CommercialText { get; init; }
    public bool SetAsDefault { get; init; }
}

public sealed record SetDefaultPdfTemplateCommand(string Code) : IRequest<PdfTemplateDto>;

public sealed record UploadPdfTemplateLogoCommand(string Code, string FileName, string ContentType, byte[] Content)
    : IRequest<PdfTemplateDto>;

public sealed record RegenerateDocumentPdfCommand(Guid DocumentId, string? Template)
    : IRequest<RegeneratePdfResultDto>;

public sealed record RenderPdfItemDto(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total);

public sealed record RenderPdfCommand : IRequest<byte[]>
{
    public string? PdfTemplate { get; init; }
    public bool ShowQr { get; init; }
    public bool ShowTaxBreakdown { get; init; } = true;
    public required string TypeLabel { get; init; }
    public required string Series { get; init; }
    public required int Number { get; init; }
    public required string FullNumber { get; init; }
    public required string IssueDate { get; init; }
    public string? ExternalReference { get; init; }
    public required string RecipientName { get; init; }
    public string RecipientIdentityType { get; init; } = "1";
    public required string RecipientIdentityNumber { get; init; }
    public string? RecipientAddress { get; init; }
    public required IReadOnlyList<RenderPdfItemDto> Items { get; init; }
    public required decimal PayableAmount { get; init; }
    public string? Observation { get; init; }
    public string? FooterText { get; init; }
}
