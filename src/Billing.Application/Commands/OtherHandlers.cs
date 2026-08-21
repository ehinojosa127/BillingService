using Billing.Application.Abstractions;
using Billing.Application.DTOs;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
using Billing.Application.Queries;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Billing.Application.Commands;

public sealed class CancelDocumentHandler(
    IDocumentRepository documentRepository,
    IDocumentSeriesRepository seriesRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IClock clock,
    IVoidedDocumentsXmlGenerator voidXmlGenerator,
    IXmlSigner xmlSigner,
    IElectronicDocumentProvider documentProvider,
    IIssuerTaxProfile taxProfile,
    ILogger<CancelDocumentHandler> logger) : IRequestHandler<CancelDocumentCommand, DocumentResultDto>
{
    public async Task<DocumentResultDto> Handle(CancelDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");

        if (!DocumentStatusMachine.CanCancel(document))
        {
            throw new ConflictException("CANCEL_NOT_ALLOWED", $"Document {document.FullNumber} cannot be voided from status '{document.Status}'.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Anulación solicitada por el emisor"
            : request.Reason.Trim();
        if (reason.Length < 3)
        {
            throw new ValidationException(["El motivo de la baja debe tener al menos 3 caracteres."]);
        }

        if (DocumentStatusMachine.RequiresSunatVoid(document))
        {
            try
            {
                await SubmitVoidAsync(document, reason, request, cancellationToken);
            }
            catch (SunatUnavailableException exception) when (!taxProfile.IsProductionEnvironment)
            {
                logger.LogWarning(
                    exception,
                    "SUNAT void unavailable in non-production for {Document}; applying local cancel.",
                    document.FullNumber);
                document.Cancel(clock.UtcNow);
                await auditLogRepository.AddAsync(AuditLog.Create(
                    AuditAction.DocumentCancelled,
                    clock.UtcNow,
                    document.Id,
                    document.ExternalSystem,
                    request.RequestedBy,
                    request.CorrelationId,
                    $"Baja local (SUNAT beta): {reason}"), cancellationToken);
            }
        }
        else
        {
            document.Cancel(clock.UtcNow);
            await auditLogRepository.AddAsync(AuditLog.Create(
                AuditAction.DocumentCancelled,
                clock.UtcNow,
                document.Id,
                document.ExternalSystem,
                request.RequestedBy,
                request.CorrelationId,
                reason), cancellationToken);
        }

        await documentRepository.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToResult(document);
    }

    private async Task SubmitVoidAsync(
        ElectronicDocument document,
        string reason,
        CancelDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var lima = clock.LimaNow;
        var issueDate = DateOnly.FromDateTime(lima.DateTime);
        var series = issueDate.ToString("MMdd");
        var number = await seriesRepository.AllocateNextNumberAsync("RA", series, cancellationToken);
        var voidId = $"RA-{issueDate:yyyyMMdd}-{number}";
        var xmlName = $"{document.IssuerRuc}-{voidId}.xml";

        var xml = voidXmlGenerator.Generate(document, voidId, issueDate, reason);
        var signed = xmlSigner.Sign(xml);
        var result = await documentProvider.SendSummaryAsync(xmlName, signed.Xml, cancellationToken);

        var submission = document.RecordAuxiliarySubmission(clock.UtcNow);
        submission.Complete(result.Status, clock.UtcNow, result.Ticket, result.ResponseCode, result.Description, result.Notes, "VoidSummary");

        await auditLogRepository.AddAsync(AuditLog.Create(
            AuditAction.VoidSubmitted,
            clock.UtcNow,
            document.Id,
            document.ExternalSystem,
            request.RequestedBy,
            request.CorrelationId,
            $"{voidId}: {result.Description}"), cancellationToken);

        if (result.Status is SunatStatus.Accepted or SunatStatus.AcceptedWithObservations)
        {
            document.Cancel(clock.UtcNow);
            await auditLogRepository.AddAsync(AuditLog.Create(
                AuditAction.DocumentCancelled,
                clock.UtcNow,
                document.Id,
                document.ExternalSystem,
                request.RequestedBy,
                request.CorrelationId,
                reason), cancellationToken);
            return;
        }

        if (result.Status is SunatStatus.Rejected)
        {
            throw new SunatRejectionException(result.Description, result.ResponseCode, result.Notes);
        }

        logger.LogInformation("Void summary for {Document} is in process. Ticket={Ticket}", document.FullNumber, result.Ticket);
    }
}

public sealed class ConsultSunatStatusHandler(
    IDocumentRepository documentRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IClock clock,
    IElectronicDocumentProvider documentProvider,
    IFileStorage fileStorage,
    ICdrParser cdrParser) : IRequestHandler<ConsultSunatStatusCommand, DocumentResultDto>
{
    public async Task<DocumentResultDto> Handle(ConsultSunatStatusCommand request, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");

        if (!DocumentStatusMachine.CanConsult(document))
        {
            throw new ConflictException("CONSULT_NOT_ALLOWED", $"Document {document.FullNumber} cannot consult SUNAT from status '{document.Status}'.");
        }

        var last = document.Submissions.OrderBy(x => x.Attempt).LastOrDefault();
        SubmissionResult result;
        if (last?.ErrorKind == "VoidSummary" && !string.IsNullOrWhiteSpace(last.Ticket))
        {
            result = await documentProvider.GetSummaryStatusAsync(last.Ticket, cancellationToken);
            last.Complete(result.Status, clock.UtcNow, last.Ticket, result.ResponseCode, result.Description, result.Notes, "VoidSummary");
            if (result.Status is SunatStatus.Accepted or SunatStatus.AcceptedWithObservations)
            {
                document.Cancel(clock.UtcNow);
            }
        }
        else if (IsTerminalForConsult(document))
        {
            result = new SubmissionResult(
                document.SunatStatus,
                last?.ResponseCode,
                last?.Description ?? "Estado ya sincronizado con SUNAT.",
                last?.Notes,
                last?.Ticket,
                null,
                null);
        }
        else
        {
            result = await documentProvider.GetStatusAsync(document, last?.Ticket, cancellationToken);
            document.ReconcileFromSunat(
                result.Status,
                result.ResponseCode,
                result.Description,
                result.Notes,
                result.Ticket,
                clock.UtcNow);
        }

        if (result.CdrZip is { Length: > 0 } && document.GetFile(GeneratedFileKind.Cdr) is null)
        {
            var parsed = cdrParser.Parse(result.CdrZip);
            var key = $"{document.IssuerRuc}/{document.DocumentTypeCode}/{document.Series}/{document.Number}/{GeneratedFileKind.Cdr}-R-{document.XmlFileName}.xml";
            var stored = await fileStorage.SaveAsync(key, "R-" + document.XmlFileName + ".xml", "application/xml", parsed.OriginalXml, cancellationToken);
            document.AddFile(GeneratedFile.Create(document.Id, GeneratedFileKind.Cdr, stored.Key, stored.FileName, stored.ContentType, clock.UtcNow));
        }

        await auditLogRepository.AddAsync(AuditLog.Create(
            AuditAction.SunatConsulted,
            clock.UtcNow,
            document.Id,
            document.ExternalSystem,
            request.RequestedBy,
            request.CorrelationId,
            result.Description), cancellationToken);
        await documentRepository.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToResult(document);
    }

    private static bool IsTerminalForConsult(ElectronicDocument document) =>
        document.Status is DocumentStatus.Accepted
            or DocumentStatus.Observed
            or DocumentStatus.Rejected
        && document.SunatStatus is SunatStatus.Accepted
            or SunatStatus.AcceptedWithObservations
            or SunatStatus.Rejected;
}

public sealed class UpsertIssuerHandler(
    IIssuerRepository issuerRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<UpsertIssuerCommand, IssuerDto>
{
    public async Task<IssuerDto> Handle(UpsertIssuerCommand request, CancellationToken cancellationToken)
    {
        var address = new Domain.ValueObjects.Address(
            request.AddressLine,
            request.Ubigeo,
            request.Department,
            request.Province,
            request.District,
            request.CountryCode,
            request.Urbanization);

        var existing = await issuerRepository.GetAsync(cancellationToken);
        if (existing is null)
        {
            existing = Issuer.Create(
                request.Ruc,
                request.LegalName,
                request.TradeName ?? request.LegalName,
                address,
                request.Email,
                request.Phone,
                request.EstablishmentCode,
                clock.UtcNow);
            await issuerRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(
                request.Ruc,
                request.LegalName,
                request.TradeName ?? request.LegalName,
                address,
                request.Email,
                request.Phone,
                request.EstablishmentCode,
                clock.UtcNow);
            await issuerRepository.UpdateAsync(existing, cancellationToken);
        }

        await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.IssuerUpdated, clock.UtcNow, details: existing.Ruc), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(existing);
    }

    internal static IssuerDto Map(Issuer issuer) => new(
        issuer.Id,
        issuer.Ruc,
        issuer.LegalName,
        issuer.TradeName,
        issuer.Address.Line,
        issuer.Address.Ubigeo,
        issuer.Address.Department,
        issuer.Address.Province,
        issuer.Address.District,
        issuer.Address.CountryCode,
        issuer.Address.Urbanization,
        issuer.EstablishmentCode,
        issuer.Email,
        issuer.Phone);
}

public sealed class CreateSeriesHandler(
    IDocumentSeriesRepository seriesRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreateSeriesCommand, SeriesDto>
{
    public async Task<SeriesDto> Handle(CreateSeriesCommand request, CancellationToken cancellationToken)
    {
        var type = Domain.Catalogs.DocumentType.FromCode(request.DocumentType);
        var existing = await seriesRepository.GetAsync(type, request.Series.ToUpperInvariant(), cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("SERIES_EXISTS", $"Series '{request.Series}' already exists for document type {type.Code}.");
        }

        var series = DocumentSeries.Create(type, request.Series, clock.UtcNow);
        await seriesRepository.AddAsync(series, cancellationToken);
        await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.SeriesCreated, clock.UtcNow, details: $"{type.Code}-{series.Series}"), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SeriesDto(series.Id, series.DocumentTypeCode, series.Series, series.LastNumber, series.IsActive);
    }
}

public sealed class UpsertPdfTemplateHandler(
    IPdfTemplateRepository templates,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<UpsertPdfTemplateCommand, PdfTemplateDto>
{
    public async Task<PdfTemplateDto> Handle(UpsertPdfTemplateCommand request, CancellationToken cancellationToken)
    {
        await Queries.GetPdfTemplatesHandler.EnsureDefaultsAsync(templates, clock, cancellationToken);
        var code = PdfTemplate.NormalizeCode(request.Code);
        var template = await templates.GetByCodeAsync(code, cancellationToken)
                       ?? throw new NotFoundException($"PDF template '{code}' was not found.");

        template.Update(request.Name, request.TradeName, request.PrimaryColor, request.FooterText, request.CommercialText, clock.UtcNow);
        if (request.SetAsDefault)
        {
            await SetDefaultAsync(templates, template, clock, cancellationToken);
        }

        await templates.UpdateAsync(template, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToTemplate(template);
    }

    internal static async Task SetDefaultAsync(
        IPdfTemplateRepository templates,
        PdfTemplate selected,
        IClock clock,
        CancellationToken cancellationToken)
    {
        foreach (var item in await templates.ListAsync(cancellationToken))
        {
            if (item.Id == selected.Id)
            {
                item.MarkDefault(clock.UtcNow);
            }
            else if (item.IsDefault)
            {
                item.ClearDefault();
            }
        }
    }
}

public sealed class SetDefaultPdfTemplateHandler(
    IPdfTemplateRepository templates,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<SetDefaultPdfTemplateCommand, PdfTemplateDto>
{
    public async Task<PdfTemplateDto> Handle(SetDefaultPdfTemplateCommand request, CancellationToken cancellationToken)
    {
        await Queries.GetPdfTemplatesHandler.EnsureDefaultsAsync(templates, clock, cancellationToken);
        var template = await templates.GetByCodeAsync(PdfTemplate.NormalizeCode(request.Code), cancellationToken)
                       ?? throw new NotFoundException($"PDF template '{request.Code}' was not found.");
        await UpsertPdfTemplateHandler.SetDefaultAsync(templates, template, clock, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToTemplate(template);
    }
}

public sealed class UploadPdfTemplateLogoHandler(
    IPdfTemplateRepository templates,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<UploadPdfTemplateLogoCommand, PdfTemplateDto>
{
    private const int MaxBytes = 2 * 1024 * 1024;

    public async Task<PdfTemplateDto> Handle(UploadPdfTemplateLogoCommand request, CancellationToken cancellationToken)
    {
        await Queries.GetPdfTemplatesHandler.EnsureDefaultsAsync(templates, clock, cancellationToken);
        var template = await templates.GetByCodeAsync(PdfTemplate.NormalizeCode(request.Code), cancellationToken)
                       ?? throw new NotFoundException($"PDF template '{request.Code}' was not found.");

        var contentType = (request.ContentType ?? string.Empty).ToLowerInvariant();
        if (contentType is not "image/png" and not "image/jpeg" and not "image/jpg")
        {
            throw new ValidationException(["El logo debe ser PNG o JPEG."]);
        }

        if (request.Content.Length == 0 || request.Content.Length > MaxBytes)
        {
            throw new ValidationException(["El logo no puede superar 2 MB."]);
        }

        var extension = contentType.Contains("png") ? "png" : "jpg";
        var key = $"branding/{template.Code.ToLowerInvariant()}/logo.{extension}";
        var stored = await storage.SaveAsync(key, request.FileName, contentType, request.Content, cancellationToken);
        template.SetLogo(stored.Key, clock.UtcNow);
        await templates.UpdateAsync(template, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToTemplate(template);
    }
}

public sealed class RegenerateDocumentPdfHandler(
    IDocumentRepository documents,
    IPdfTemplateResolver templateResolver,
    DocumentPdfStore documentPdfStore,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegenerateDocumentPdfCommand, RegeneratePdfResultDto>
{
    public async Task<RegeneratePdfResultDto> Handle(RegenerateDocumentPdfCommand request, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(request.DocumentId, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");

        var xmlHashBefore = await HashFileAsync(document, GeneratedFileKind.SignedXml, cancellationToken);
        var cdrHashBefore = await HashFileAsync(document, GeneratedFileKind.Cdr, cancellationToken);
        var seriesBefore = document.Series;
        var numberBefore = document.Number;
        var sunatBefore = document.SunatStatus;
        var statusBefore = document.Status;

        var template = templateResolver.Resolve(request.Template);
        var result = await documentPdfStore.RegenerateAsync(document, template, cancellationToken);
        await documents.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var xmlHashAfter = await HashFileAsync(document, GeneratedFileKind.SignedXml, cancellationToken);
        var cdrHashAfter = await HashFileAsync(document, GeneratedFileKind.Cdr, cancellationToken);
        if (xmlHashBefore != xmlHashAfter || cdrHashBefore != cdrHashAfter
            || document.Series != seriesBefore || document.Number != numberBefore
            || document.SunatStatus != sunatBefore || document.Status != statusBefore)
        {
            throw new InternalApplicationException("PDF regeneration altered tributary artifacts.");
        }

        return new RegeneratePdfResultDto(
            document.Id,
            result.TemplateType.ToCode(),
            result.GeneratedAt,
            true,
            result.FileName);
    }

    private async Task<string?> HashFileAsync(ElectronicDocument document, GeneratedFileKind kind, CancellationToken cancellationToken)
    {
        var file = document.GetFile(kind);
        if (file is null)
        {
            return null;
        }

        var stored = await fileStorage.GetAsync(file.StorageKey, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stored.Content));
    }
}
