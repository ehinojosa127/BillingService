using Billing.Application.DTOs;
using MediatR;

namespace Billing.Application.Queries;

public sealed record GetDocumentQuery(Guid Id) : IRequest<DocumentResultDto>;

public sealed record GetDocumentsQuery(
    string? DocumentType,
    string? Series,
    string? Status,
    string? SunatStatus,
    string? ExternalReference,
    string? ExternalId,
    string? ExternalSystem,
    string? Search,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    decimal? MinAmount,
    decimal? MaxAmount,
    int Skip,
    int Take) : IRequest<PagedResultDto<DocumentListItemDto>>;

public sealed record GetDocumentStatusQuery(Guid Id) : IRequest<DocumentStatusDto>;

public sealed record GetDocumentFileQuery(Guid Id, string Kind, string? Template = null) : IRequest<FileDownloadDto>;

public sealed record GetIssuerQuery : IRequest<IssuerDto>;

public sealed record GetCapabilitiesQuery : IRequest<IssuerCapabilitiesDto>;

public sealed record GetSeriesQuery : IRequest<IReadOnlyList<SeriesDto>>;

public sealed record GetPdfTemplatesQuery : IRequest<IReadOnlyList<PdfTemplateDto>>;

public sealed record FileDownloadDto(string FileName, string ContentType, byte[] Content);
