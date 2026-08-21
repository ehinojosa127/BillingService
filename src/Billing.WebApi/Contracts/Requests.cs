using Billing.Application.Commands;

namespace Billing.WebApi.Contracts;

public sealed record IssueDocumentRequest
{
    public required string Series { get; init; }
    public required RecipientRequest Recipient { get; init; }
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
    public decimal GlobalDiscount { get; init; }
    public decimal GlobalCharge { get; init; }
    public RelatedDocumentRequest? RelatedDocument { get; init; }
    public ShippingGuideRequest? ShippingGuide { get; init; }
    public required IReadOnlyList<IssueItemRequest> Items { get; init; }
    public string? PdfTemplate { get; init; }
}

public sealed record RecipientRequest
{
    public required string IdentityType { get; init; }
    public required string IdentityNumber { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
    public string? Email { get; init; }
}

public sealed record IssueItemRequest
{
    public string? Code { get; init; }
    public required string Description { get; init; }
    public required decimal Quantity { get; init; }
    public string UnitCode { get; init; } = "NIU";
    public required decimal UnitValue { get; init; }
    public decimal? TaxInclusiveUnitPrice { get; init; }
    public decimal Discount { get; init; }
    public string TaxAffectation { get; init; } = "10";
}

public sealed record RelatedDocumentRequest
{
    public required string DocumentType { get; init; }
    public required string Series { get; init; }
    public required int Number { get; init; }
    public required string ReasonCode { get; init; }
    public string? ReasonDescription { get; init; }
}

public sealed record AddressRequest
{
    public required string Line { get; init; }
    public required string Ubigeo { get; init; }
    public required string Department { get; init; }
    public required string Province { get; init; }
    public required string District { get; init; }
    public string CountryCode { get; init; } = "PE";
    public string? Urbanization { get; init; }
}

public sealed record ShippingGuideRequest
{
    public required string TransferReason { get; init; }
    public required string TransportMode { get; init; }
    public required DateOnly TransferStartDate { get; init; }
    public required decimal GrossWeightKg { get; init; }
    public required int PackageCount { get; init; }
    public required AddressRequest Origin { get; init; }
    public required AddressRequest Destination { get; init; }
    public string? CarrierRuc { get; init; }
    public string? CarrierName { get; init; }
    public string? VehiclePlate { get; init; }
    public string? DriverLicense { get; init; }
    public string? DriverDocumentType { get; init; }
    public string? DriverDocumentNumber { get; init; }
    public string? Observation { get; init; }
}

public sealed record UpsertIssuerRequest
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

public sealed record CreateSeriesRequest(string DocumentType, string Series);

public static class RequestMapping
{
    public static IssueDocumentCommand ToCommand(this IssueDocumentRequest request, string documentType, HttpContext http)
    {
        return new IssueDocumentCommand
        {
            DocumentType = documentType,
            Series = request.Series,
            RecipientIdentityType = request.Recipient.IdentityType,
            RecipientIdentityNumber = request.Recipient.IdentityNumber,
            RecipientName = request.Recipient.Name,
            RecipientAddress = request.Recipient.Address,
            RecipientEmail = request.Recipient.Email,
            Currency = request.Currency,
            OperationType = request.OperationType,
            PaymentForm = request.PaymentForm,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            Observation = request.Observation,
            ExternalSystem = request.ExternalSystem,
            ExternalEntity = request.ExternalEntity,
            ExternalId = request.ExternalId,
            ExternalReference = request.ExternalReference,
            RequestedBy = request.RequestedBy,
            IdempotencyKey = http.Request.Headers[Billing.Shared.BillingHeaders.IdempotencyKey].FirstOrDefault(),
            CorrelationId = http.Items[Billing.Shared.BillingHeaders.CorrelationId]?.ToString(),
            GlobalDiscount = request.GlobalDiscount,
            GlobalCharge = request.GlobalCharge,
            RelatedDocument = request.RelatedDocument is null ? null : new RelatedDocumentDto(
                request.RelatedDocument.DocumentType,
                request.RelatedDocument.Series,
                request.RelatedDocument.Number,
                request.RelatedDocument.ReasonCode,
                request.RelatedDocument.ReasonDescription),
            ShippingGuide = request.ShippingGuide is null ? null : MapGuide(request.ShippingGuide),
            Items = request.Items.Select(item => new IssueItemDto(
                item.Code,
                item.Description,
                item.Quantity,
                item.UnitCode,
                item.UnitValue,
                item.Discount,
                item.TaxAffectation,
                item.TaxInclusiveUnitPrice)).ToArray(),
            PdfTemplate = request.PdfTemplate
        };
    }

    private static ShippingGuideDto MapGuide(ShippingGuideRequest request) =>
        new(
            request.TransferReason,
            request.TransportMode,
            request.TransferStartDate,
            request.GrossWeightKg,
            request.PackageCount,
            new AddressDto(request.Origin.Line, request.Origin.Ubigeo, request.Origin.Department, request.Origin.Province, request.Origin.District, request.Origin.CountryCode, request.Origin.Urbanization),
            new AddressDto(request.Destination.Line, request.Destination.Ubigeo, request.Destination.Department, request.Destination.Province, request.Destination.District, request.Destination.CountryCode, request.Destination.Urbanization),
            request.CarrierRuc,
            request.CarrierName,
            request.VehiclePlate,
            request.DriverLicense,
            request.DriverDocumentType,
            request.DriverDocumentNumber,
            request.Observation);
}
