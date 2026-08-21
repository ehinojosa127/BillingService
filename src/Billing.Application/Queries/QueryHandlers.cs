using Billing.Application.Abstractions;
using Billing.Application.DTOs;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Services;
using MediatR;

namespace Billing.Application.Queries;

public sealed class GetDocumentHandler(IDocumentRepository documents)
    : IRequestHandler<GetDocumentQuery, DocumentResultDto>
{
    public async Task<DocumentResultDto> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.Id}' was not found.");
        return DocumentMapper.ToResult(document);
    }
}

public sealed class GetDocumentsHandler(IDocumentRepository documents)
    : IRequestHandler<GetDocumentsQuery, PagedResultDto<DocumentListItemDto>>
{
    public async Task<PagedResultDto<DocumentListItemDto>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var take = request.Take <= 0 ? 50 : Math.Min(request.Take, 200);
        var skip = Math.Max(request.Skip, 0);
        var (items, total) = await documents.SearchAsync(
            new DocumentSearchFilter(
                NormalizeDocumentType(request.DocumentType),
                request.Series,
                request.Status,
                request.SunatStatus,
                request.ExternalReference,
                request.ExternalId,
                request.ExternalSystem,
                request.Search,
                request.DateFrom,
                request.DateTo,
                request.MinAmount,
                request.MaxAmount,
                skip,
                take),
            cancellationToken);
        return new PagedResultDto<DocumentListItemDto>(items.Select(DocumentMapper.ToListItem).ToArray(), total, skip, take);
    }

    private static string? NormalizeDocumentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "invoice" or "factura" => "01",
            "receipt" or "boleta" => "03",
            "creditnote" or "credit-note" or "nota de crédito" => "07",
            "debitnote" or "debit-note" or "nota de débito" => "08",
            "shippingguide" or "shipping-guide" or "guia" => "09",
            _ => value.Trim()
        };
    }
}

public sealed class GetDocumentStatusHandler(IDocumentRepository documents)
    : IRequestHandler<GetDocumentStatusQuery, DocumentStatusDto>
{
    public async Task<DocumentStatusDto> Handle(GetDocumentStatusQuery request, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.Id}' was not found.");
        var last = document.Submissions.OrderBy(x => x.Attempt).LastOrDefault();
        return new DocumentStatusDto(
            document.Id,
            DocumentMapper.ToApiStatus(document.Status),
            DocumentMapper.ToApiSunatStatus(document.SunatStatus),
            last?.ResponseCode,
            last?.Description,
            last?.Ticket,
            DocumentStatusMachine.CanRetrySubmission(document),
            last?.Notes,
            document.Submissions.Count,
            last?.StartedAt.ToString("O"),
            DocumentStatusMachine.CanCancel(document),
            DocumentStatusMachine.CanConsult(document));
    }
}

public sealed class GetDocumentFileHandler(
    IDocumentRepository documents,
    IFileStorage storage,
    IPdfTemplateResolver templateResolver,
    DocumentPdfStore documentPdfStore,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GetDocumentFileQuery, FileDownloadDto>
{
    public async Task<FileDownloadDto> Handle(GetDocumentFileQuery request, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(request.Id, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.Id}' was not found.");

        var kind = request.Kind.ToLowerInvariant() switch
        {
            "xml" => GeneratedFileKind.SignedXml,
            "pdf" => GeneratedFileKind.Pdf,
            "cdr" => GeneratedFileKind.Cdr,
            _ => throw new Billing.Application.Exceptions.ValidationException(["Unsupported file kind."])
        };

        if (kind == GeneratedFileKind.Pdf)
        {
            var existing = document.GetFile(GeneratedFileKind.Pdf);
            if (existing is not null)
            {
                var storedExisting = await storage.GetAsync(existing.StorageKey, cancellationToken)
                                     ?? throw new NotFoundException("The stored file could not be found.");
                return new FileDownloadDto(existing.FileName, existing.ContentType, storedExisting.Content);
            }

            var template = templateResolver.Resolve(request.Template);
            var pdf = await documentPdfStore.SaveAsync(document, template, cancellationToken);
            try
            {
                await documents.UpdateAsync(document, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                return new FileDownloadDto($"{document.XmlFileName}.{template.ToCode()}.pdf", "application/pdf", pdf);
            }

            var created = document.GetFile(GeneratedFileKind.Pdf)
                          ?? throw new NotFoundException($"The pdf file is not available for document '{document.FullNumber}'.");
            return new FileDownloadDto(created.FileName, created.ContentType, pdf);
        }

        var file = document.GetFile(kind) ?? (kind == GeneratedFileKind.SignedXml ? document.GetFile(GeneratedFileKind.Xml) : null)
                   ?? throw new NotFoundException($"The {request.Kind} file is not available for document '{document.FullNumber}'.");

        var stored = await storage.GetAsync(file.StorageKey, cancellationToken)
                     ?? throw new NotFoundException("The stored file could not be found.");
        return new FileDownloadDto(file.FileName, file.ContentType, stored.Content);
    }
}

public sealed class GetIssuerHandler(IIssuerRepository issuers) : IRequestHandler<GetIssuerQuery, IssuerDto>
{
    public async Task<IssuerDto> Handle(GetIssuerQuery request, CancellationToken cancellationToken)
    {
        var issuer = await issuers.GetAsync(cancellationToken)
                     ?? throw new NotFoundException("Issuer has not been configured.");
        return Commands.UpsertIssuerHandler.Map(issuer);
    }
}

public sealed class GetCapabilitiesHandler(IIssuerTaxProfile taxProfile)
    : IRequestHandler<GetCapabilitiesQuery, IssuerCapabilitiesDto>
{
    public Task<IssuerCapabilitiesDto> Handle(GetCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var regime = taxProfile.Regime;
        var taxpayer = taxProfile.TaxpayerType;
        var allowed = regime.AllowedDocumentTypes.Select(type => type.Code).ToArray();
        return Task.FromResult(new IssuerCapabilitiesDto(
            regime.Code,
            regime.Name,
            taxpayer.Code,
            taxpayer.Name,
            allowed,
            regime.CanIssue(DocumentType.Invoice),
            regime.CanIssue(DocumentType.Receipt)));
    }
}

public sealed class GetSeriesHandler(IDocumentSeriesRepository series)
    : IRequestHandler<GetSeriesQuery, IReadOnlyList<SeriesDto>>
{
    public async Task<IReadOnlyList<SeriesDto>> Handle(GetSeriesQuery request, CancellationToken cancellationToken)
    {
        var items = await series.ListAsync(cancellationToken);
        return items.Select(item => new SeriesDto(item.Id, item.DocumentTypeCode, item.Series, item.LastNumber, item.IsActive)).ToArray();
    }
}

public sealed class GetPdfTemplatesHandler(IPdfTemplateRepository templates, IClock clock, IUnitOfWork unitOfWork)
    : IRequestHandler<GetPdfTemplatesQuery, IReadOnlyList<PdfTemplateDto>>
{
    public async Task<IReadOnlyList<PdfTemplateDto>> Handle(GetPdfTemplatesQuery request, CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(templates, clock, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var items = await templates.ListAsync(cancellationToken);
        return items.Select(DocumentMapper.ToTemplate).ToArray();
    }

    internal static async Task EnsureDefaultsAsync(IPdfTemplateRepository templates, IClock clock, CancellationToken cancellationToken)
    {
        var existing = await templates.ListAsync(cancellationToken);
        if (existing.All(x => x.Code != PdfTemplate.DefaultCode))
        {
            var created = PdfTemplate.Create(PdfTemplate.DefaultCode, "Plantilla predeterminada", existing.Count == 0, clock.UtcNow);
            created.Update(
                created.Name,
                null,
                "#1F4E79",
                "Representación impresa del comprobante electrónico. Este PDF no altera el XML enviado a SUNAT.",
                null,
                clock.UtcNow);
            await templates.AddAsync(created, cancellationToken);
        }

        if (existing.All(x => x.Code != PdfTemplate.CustomCode))
        {
            await templates.AddAsync(
                PdfTemplate.Create(PdfTemplate.CustomCode, "Plantilla personalizada", false, clock.UtcNow),
                cancellationToken);
        }
    }
}
