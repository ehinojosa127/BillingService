using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Application.DTOs;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Exceptions;
using Billing.Domain.Services;
using Billing.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Billing.Application.Commands;

public sealed class IssueDocumentHandler(
    IIssuerRepository issuerRepository,
    IDocumentSeriesRepository seriesRepository,
    IDocumentRepository documentRepository,
    IAuditLogRepository auditLogRepository,
    IIdempotencyStore idempotencyStore,
    IUnitOfWork unitOfWork,
    IClock clock,
    IXmlDocumentGenerator xmlGenerator,
    IXmlSigner xmlSigner,
    IElectronicDocumentProvider documentProvider,
    IFileStorage fileStorage,
    ICdrParser cdrParser,
    IPdfTemplateResolver pdfTemplateResolver,
    DocumentPdfStore documentPdfStore,
    IIssuerTaxProfile taxProfile,
    ILogger<IssueDocumentHandler> logger) : IRequestHandler<IssueDocumentCommand, DocumentResultDto>
{
    public async Task<DocumentResultDto> Handle(IssueDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await idempotencyStore.GetAsync(request.IdempotencyKey, cancellationToken);
            var hash = HashRequest(request);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, hash, StringComparison.Ordinal))
                {
                    throw new ConflictException("IDEMPOTENCY_CONFLICT", "The Idempotency-Key was reused with a different payload.");
                }

                return JsonSerializer.Deserialize<DocumentResultDto>(existing.ResponsePayload)
                       ?? throw new InternalApplicationException("Stored idempotent response could not be deserialized.");
            }
        }

        var issuer = await issuerRepository.GetAsync(cancellationToken)
                     ?? throw new NotFoundException("Issuer has not been configured.");

        ElectronicDocument document;
        try
        {
            document = await CreateDocumentAsync(request, issuer, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            throw new Exceptions.ValidationException([ex.Message]);
        }

        await ProcessDocumentAsync(document, request, cancellationToken);

        var result = DocumentMapper.ToResult(document);
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            await idempotencyStore.SaveAsync(
                IdempotencyRecord.Create(
                    request.IdempotencyKey,
                    HashRequest(request),
                    document.Id,
                    JsonSerializer.Serialize(result),
                    201,
                    clock.UtcNow),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task<ElectronicDocument> CreateDocumentAsync(
        IssueDocumentCommand request,
        Issuer issuer,
        CancellationToken cancellationToken)
    {
        var type = DocumentType.FromCode(request.DocumentType);
        taxProfile.Regime.EnsureCanIssue(type);
        var currency = CurrencyCode.FromCode(request.Currency);
        var operation = OperationTypeCode.FromCode(request.OperationType);
        var paymentForm = request.PaymentForm.Equals("credit", StringComparison.OrdinalIgnoreCase)
            ? PaymentForm.Credit
            : PaymentForm.Cash;
        var recipient = new IdentityDocument(
            IdentityDocumentType.FromCode(request.RecipientIdentityType),
            request.RecipientIdentityNumber);

        RelatedDocument? related = null;
        if (request.RelatedDocument is not null)
        {
            var relatedType = DocumentType.FromCode(request.RelatedDocument.DocumentType);
            var reason = type == DocumentType.CreditNote
                ? NoteReasonCode.ForCreditNote(request.RelatedDocument.ReasonCode)
                : NoteReasonCode.ForDebitNote(request.RelatedDocument.ReasonCode);
            related = new RelatedDocument(
                relatedType,
                request.RelatedDocument.Series,
                request.RelatedDocument.Number,
                reason,
                request.RelatedDocument.ReasonDescription);
        }

        ShippingGuideInfo? guide = null;
        if (request.ShippingGuide is not null)
        {
            guide = MapGuide(request.ShippingGuide);
        }

        var drafts = request.Items.Select(item => new DocumentItemDraft(
            item.Code,
            item.Description,
            item.Quantity,
            item.UnitCode,
            ResolveUnitValue(item),
            item.Discount,
            TaxAffectationCode.FromCode(item.TaxAffectation))).ToArray();

        ElectronicDocument? created = null;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var number = await seriesRepository.AllocateNextNumberAsync(type, request.Series, ct);
            created = ElectronicDocument.Issue(
                issuer,
                type,
                request.Series,
                number,
                ResolveIssuedAt(clock, request.IssueDate),
                currency,
                operation,
                paymentForm,
                recipient,
                request.RecipientName,
                request.RecipientAddress,
                request.RecipientEmail,
                drafts,
                related,
                guide,
                new ExternalReference(request.ExternalSystem, request.ExternalReference, request.ExternalEntity, request.ExternalId),
                request.RequestedBy,
                request.Observation,
                request.DueDate,
                request.GlobalDiscount,
                request.GlobalCharge);

            await documentRepository.AddAsync(created, ct);
            await auditLogRepository.AddAsync(AuditLog.Create(
                AuditAction.DocumentCreated,
                clock.UtcNow,
                created.Id,
                request.ExternalSystem,
                request.RequestedBy,
                request.CorrelationId,
                created.FullNumber), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        return created ?? throw new InternalApplicationException("The document was not created.");
    }

    internal async Task ProcessDocumentAsync(
        ElectronicDocument document,
        IssueDocumentCommand? request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing electronic document {DocumentId} {DocumentType} {Series}-{Number} ExternalSystem={ExternalSystem} ExternalReference={ExternalReference}",
            document.Id,
            document.DocumentTypeCode,
            document.Series,
            document.Number,
            document.ExternalSystem,
            document.ExternalReference);

        try
        {
            var xml = xmlGenerator.Generate(document);
            document.MarkGenerated(clock.UtcNow);
            await SaveFileAsync(document, GeneratedFileKind.Xml, document.XmlFileName + ".xml", "application/xml", xml, cancellationToken);
            await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.XmlGenerated, clock.UtcNow, document.Id, document.ExternalSystem, request?.RequestedBy, request?.CorrelationId), cancellationToken);
            await PersistAsync(document, cancellationToken);

            var signed = xmlSigner.Sign(xml);
            document.MarkSigned(signed.DigestValue, clock.UtcNow);
            await SaveFileAsync(document, GeneratedFileKind.SignedXml, document.XmlFileName + ".xml", "application/xml", signed.Xml, cancellationToken);
            await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.DocumentSigned, clock.UtcNow, document.Id, document.ExternalSystem, request?.RequestedBy, request?.CorrelationId), cancellationToken);
            await PersistAsync(document, cancellationToken);

            var submission = document.StartSubmission(clock.UtcNow);
            await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.SubmissionStarted, clock.UtcNow, document.Id, document.ExternalSystem, request?.RequestedBy, request?.CorrelationId), cancellationToken);
            await PersistAsync(document, cancellationToken);

            var simulation = BillingTestSimulation.Resolve(
                request?.Observation ?? document.Observation,
                taxProfile.IsProductionEnvironment);
            if (simulation != BillingTestSimulationMode.None)
            {
                BillingTestSimulation.Apply(document, submission, simulation, clock.UtcNow);
                await auditLogRepository.AddAsync(AuditLog.Create(
                    AuditAction.SubmissionSent,
                    clock.UtcNow,
                    document.Id,
                    document.ExternalSystem,
                    request?.RequestedBy,
                    request?.CorrelationId,
                    $"Simulación de prueba: {simulation}"), cancellationToken);
            }
            else
            {
                var submitResult = await documentProvider.SubmitAsync(document, signed.Xml, cancellationToken);
                await ApplySubmissionResultAsync(document, submission, submitResult, AuditAction.SubmissionSent, cancellationToken);
            }
            await PersistAsync(document, cancellationToken);

            await TryGeneratePdfAsync(document, request, cancellationToken);
        }
        catch (SunatRejectionException ex)
        {
            var last = document.Submissions.Last();
            if (SunatResponseCodes.IsAlreadyReported(ex.ResponseCode, ex.Message))
            {
                document.ApplySunatResult(
                    last,
                    SunatStatus.Accepted,
                    ex.ResponseCode,
                    ex.Message,
                    ex.Notes,
                    null,
                    null,
                    clock.UtcNow);
                await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.DocumentAccepted, clock.UtcNow, document.Id, document.ExternalSystem, request?.RequestedBy, request?.CorrelationId, ex.Message), cancellationToken);
            }
            else
            {
                document.ApplySunatResult(
                    last,
                    SunatStatus.Rejected,
                    ex.ResponseCode,
                    ex.Message,
                    ex.Notes,
                    null,
                    "SunatRejection",
                    clock.UtcNow);
                await auditLogRepository.AddAsync(AuditLog.Create(AuditAction.DocumentRejected, clock.UtcNow, document.Id, document.ExternalSystem, request?.RequestedBy, request?.CorrelationId, ex.Message), cancellationToken);
            }

            await PersistAsync(document, cancellationToken);
            await TryGeneratePdfAsync(document, request, cancellationToken);
        }
        catch (Exception ex) when (ex is SunatUnavailableException or TransientCommunicationException)
        {
            document.MarkFailed(ex is SunatUnavailableException ? "SunatUnavailable" : "TransientCommunicationError", ex.Message, clock.UtcNow);
            await PersistAsync(document, cancellationToken);
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not BillingApplicationException)
        {
            logger.LogError(ex, "Unexpected error while processing document {DocumentId}", document.Id);
            try
            {
                document.MarkFailed("InternalError", "An internal error occurred while processing the document.", clock.UtcNow);
                await PersistAsync(document, cancellationToken);
            }
            catch (Exception persistEx)
            {
                logger.LogError(persistEx, "Could not persist failure state for document {DocumentId}", document.Id);
            }

            if (document.Status is DocumentStatus.Accepted or DocumentStatus.Observed or DocumentStatus.Rejected)
            {
                return;
            }

            throw new InternalApplicationException("An internal error occurred while processing the document.", ex);
        }
    }

    private async Task TryGeneratePdfAsync(ElectronicDocument document, IssueDocumentCommand? request, CancellationToken cancellationToken)
    {
        try
        {
            if (document.GetFile(GeneratedFileKind.Pdf) is not null)
            {
                return;
            }

            var template = pdfTemplateResolver.Resolve(request?.PdfTemplate);
            await documentPdfStore.SaveAsync(document, template, cancellationToken);
            await PersistAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF generation failed for document {DocumentId}; SUNAT status was kept.", document.Id);
        }
    }

    private async Task PersistAsync(ElectronicDocument document, CancellationToken cancellationToken)
    {
        await documentRepository.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal async Task ApplySubmissionResultAsync(
        ElectronicDocument document,
        DocumentSubmission submission,
        SubmissionResult submitResult,
        AuditAction sentAction,
        CancellationToken cancellationToken)
    {
        await auditLogRepository.AddAsync(AuditLog.Create(sentAction, clock.UtcNow, document.Id, document.ExternalSystem, document.RequestedBy, null, submitResult.Description), cancellationToken);

        if (submitResult.CdrZip is { Length: > 0 })
        {
            await SaveFileAsync(document, GeneratedFileKind.Zip, "R-" + document.XmlFileName + ".zip", "application/zip", submitResult.CdrZip, cancellationToken);
            var parsed = cdrParser.Parse(submitResult.CdrZip);
            await SaveFileAsync(document, GeneratedFileKind.Cdr, "R-" + document.XmlFileName + ".xml", "application/xml", parsed.OriginalXml, cancellationToken);
            var status = SunatResponseCodes.IsAlreadyReported(parsed.ResponseCode, parsed.Description)
                ? SunatStatus.Accepted
                : parsed.Status;
            document.ApplySunatResult(submission, status, parsed.ResponseCode, parsed.Description, parsed.Notes, submitResult.Ticket, null, clock.UtcNow);
        }
        else
        {
            var status = SunatResponseCodes.IsAlreadyReported(submitResult.ResponseCode, submitResult.Description)
                ? SunatStatus.Accepted
                : submitResult.Status;
            document.ApplySunatResult(
                submission,
                status,
                submitResult.ResponseCode,
                submitResult.Description,
                submitResult.Notes,
                submitResult.Ticket,
                null,
                clock.UtcNow);
        }

        var audit = document.SunatStatus switch
        {
            SunatStatus.Accepted => AuditAction.DocumentAccepted,
            SunatStatus.AcceptedWithObservations => AuditAction.DocumentObserved,
            SunatStatus.Rejected => AuditAction.DocumentRejected,
            _ => sentAction
        };
        await auditLogRepository.AddAsync(AuditLog.Create(audit, clock.UtcNow, document.Id, document.ExternalSystem, document.RequestedBy, null, submitResult.Description), cancellationToken);
    }

    private async Task SaveFileAsync(
        ElectronicDocument document,
        GeneratedFileKind kind,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var key = $"{document.IssuerRuc}/{document.DocumentTypeCode}/{document.Series}/{document.Number}/{kind}-{fileName}";
        var stored = await fileStorage.SaveAsync(key, fileName, contentType, content, cancellationToken);
        document.AddFile(GeneratedFile.Create(document.Id, kind, stored.Key, fileName, contentType, clock.UtcNow));
    }

    private static ShippingGuideInfo MapGuide(ShippingGuideDto dto) =>
        new(
            TransferReasonCode.FromCode(dto.TransferReason),
            TransportModeCode.FromCode(dto.TransportMode),
            dto.TransferStartDate,
            dto.GrossWeightKg,
            dto.PackageCount,
            new Address(dto.Origin.Line, dto.Origin.Ubigeo, dto.Origin.Department, dto.Origin.Province, dto.Origin.District, dto.Origin.CountryCode, dto.Origin.Urbanization),
            new Address(dto.Destination.Line, dto.Destination.Ubigeo, dto.Destination.Department, dto.Destination.Province, dto.Destination.District, dto.Destination.CountryCode, dto.Destination.Urbanization),
            dto.CarrierRuc,
            dto.CarrierName,
            dto.VehiclePlate,
            dto.DriverLicense,
            dto.DriverDocumentNumber,
            string.IsNullOrWhiteSpace(dto.DriverDocumentType) ? null : IdentityDocumentType.FromCode(dto.DriverDocumentType),
            dto.Observation);

    private static decimal ResolveUnitValue(IssueItemDto item)
    {
        if (item.TaxInclusiveUnitPrice is not decimal inclusive || inclusive <= 0)
        {
            return item.UnitValue;
        }

        var lineTotal = Money.Round(item.Quantity * inclusive);
        if (item.Quantity <= 0)
        {
            return 0m;
        }

        return lineTotal / item.Quantity / (1m + TaxRates.Igv);
    }

    private static DateTimeOffset ResolveIssuedAt(IClock clock, DateOnly? issueDate)
    {
        var lima = clock.LimaNow;
        if (issueDate is null)
        {
            return lima;
        }

        return new DateTimeOffset(issueDate.Value, TimeOnly.FromTimeSpan(lima.TimeOfDay), lima.Offset);
    }

    private static string HashRequest(IssueDocumentCommand request)
    {
        var json = JsonSerializer.Serialize(request with { IdempotencyKey = null, CorrelationId = null });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
