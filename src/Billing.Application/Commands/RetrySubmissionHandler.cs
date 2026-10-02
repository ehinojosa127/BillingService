using Billing.Application.Abstractions;
using Billing.Application.DTOs;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Billing.Application.Commands;

public sealed class RetrySubmissionHandler(
    IDocumentRepository documentRepository,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork,
    IClock clock,
    IFileStorage fileStorage,
    IElectronicDocumentProvider documentProvider,
    IXmlDocumentGenerator xmlGenerator,
    IXmlSigner xmlSigner,
    ICdrParser cdrParser,
    IPdfTemplateResolver pdfTemplateResolver,
    DocumentPdfStore documentPdfStore,
    IIssuerTaxProfile taxProfile,
    ILogger<RetrySubmissionHandler> logger) : IRequestHandler<RetrySubmissionCommand, DocumentResultDto>
{
    public async Task<DocumentResultDto> Handle(RetrySubmissionCommand request, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken)
                       ?? throw new NotFoundException($"Document '{request.DocumentId}' was not found.");

        if (!DocumentStatusMachine.CanRetrySubmission(document))
        {
            throw new ConflictException("RETRY_NOT_ALLOWED", $"Document {document.FullNumber} cannot be retried from status '{document.Status}' / SUNAT '{document.SunatStatus}'.");
        }

        await auditLogRepository.AddAsync(AuditLog.Create(
            AuditAction.SubmissionRetried,
            clock.UtcNow,
            document.Id,
            document.ExternalSystem,
            request.RequestedBy,
            request.CorrelationId), cancellationToken);

        var signed = document.GetFile(GeneratedFileKind.SignedXml);
        byte[] signedXml;
        if (signed is not null)
        {
            var stored = await fileStorage.GetAsync(signed.StorageKey, cancellationToken)
                         ?? throw new NotFoundException("The signed XML is no longer available.");
            signedXml = stored.Content;
        }
        else
        {
            var xml = xmlGenerator.Generate(document);
            if (document.Status is DocumentStatus.Failed or DocumentStatus.Draft)
            {
                document.MarkGenerated(clock.UtcNow);
            }

            var signedResult = xmlSigner.Sign(xml);
            if (document.Status is DocumentStatus.Generated or DocumentStatus.Failed)
            {
                document.MarkSigned(signedResult.DigestValue, clock.UtcNow);
            }

            signedXml = signedResult.Xml;
            await SaveRetryFileAsync(document, GeneratedFileKind.Xml, document.XmlFileName + ".xml", "application/xml", xml, cancellationToken);
            await SaveRetryFileAsync(document, GeneratedFileKind.SignedXml, document.XmlFileName + ".xml", "application/xml", signedXml, cancellationToken);
        }

        try
        {
            var simulation = BillingTestSimulation.Resolve(document.Observation, taxProfile.IsProductionEnvironment);
            if (simulation != BillingTestSimulationMode.None)
            {
                var submission = document.StartSubmission(clock.UtcNow);
                BillingTestSimulation.Apply(document, submission, simulation, clock.UtcNow);
            }
            else if (await TryRecoverFromConsultAsync(document, cancellationToken))
            {
                logger.LogInformation(
                    "Retry for {Document} recovered via getStatusCdr without resending ZIP. Status={Status} Sunat={Sunat}",
                    document.FullNumber,
                    document.Status,
                    document.SunatStatus);
            }
            else
            {
                var submission = document.StartSubmission(clock.UtcNow);
                var result = await documentProvider.SubmitAsync(document, signedXml, cancellationToken);
                await ApplySubmissionResultAsync(document, submission, result, cancellationToken);
            }

            if (document.GetFile(GeneratedFileKind.Pdf) is null
                && document.Status is DocumentStatus.Accepted or DocumentStatus.Observed or DocumentStatus.Rejected or DocumentStatus.Sent)
            {
                await GeneratePdfAsync(document, cancellationToken);
            }
        }
        catch (SunatRejectionException ex) when (SunatResponseCodes.IsAlreadyReported(ex.ResponseCode, ex.Message))
        {
            var submission = document.Submissions.LastOrDefault() ?? document.StartSubmission(clock.UtcNow);
            document.ApplySunatResult(submission, SunatStatus.Accepted, ex.ResponseCode, ex.Message, ex.Notes, null, null, clock.UtcNow);
            if (document.GetFile(GeneratedFileKind.Pdf) is null)
            {
                await GeneratePdfAsync(document, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is SunatUnavailableException or TransientCommunicationException)
        {
            if (SunatResponseCodes.IsInProcess(null, ex.Message))
            {
                var submission = document.Submissions.LastOrDefault() ?? document.StartSubmission(clock.UtcNow);
                document.ApplySunatResult(submission, SunatStatus.InProcess, "0140", ex.Message, null, null, null, clock.UtcNow);
            }
            else
            {
                logger.LogWarning(ex, "Retry submission failed for {DocumentId}", document.Id);
                document.MarkFailed(ex.GetType().Name, ex.Message, clock.UtcNow);
            }
        }

        await documentRepository.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToResult(document);
    }

    /// <summary>
    /// Recover CDR / terminal status without resending.
    /// Boletas (03): getStatusCdr is NOT available in SUNAT — only sendBill returns the CDR.
    /// Never skip sendBill for receipts based on consult alone.
    /// </summary>
    private async Task<bool> TryRecoverFromConsultAsync(ElectronicDocument document, CancellationToken cancellationToken)
    {
        if (document.Type.IsShippingGuide || document.Type == DocumentType.Receipt)
        {
            if (document.Type == DocumentType.Receipt)
            {
                logger.LogInformation(
                    "Retry for boleta {Document}: skipping getStatusCdr (SUNAT only supports it for facturas/NC/ND). Will resend ZIP.",
                    document.FullNumber);
            }

            return false;
        }

        SubmissionResult consult;
        try
        {
            consult = await documentProvider.GetStatusAsync(document, null, cancellationToken);
        }
        catch (Exception ex) when (ex is SunatUnavailableException or TransientCommunicationException)
        {
            if (SunatResponseCodes.IsInProcess(null, ex.Message) && LooksAlreadyReceivedBySunat(document))
            {
                var submission = document.StartSubmission(clock.UtcNow);
                document.ApplySunatResult(submission, SunatStatus.InProcess, "0140", ex.Message, null, null, null, clock.UtcNow);
                return true;
            }

            logger.LogInformation(ex, "Consult before retry returned no recoverable state for {Document}; will resend.", document.FullNumber);
            return false;
        }

        if (consult.CdrZip is { Length: > 0 }
            || consult.Status is SunatStatus.Accepted
                or SunatStatus.AcceptedWithObservations
                or SunatStatus.Rejected
            || SunatResponseCodes.IsAlreadyReported(consult.ResponseCode, consult.Description))
        {
            var submission = document.StartSubmission(clock.UtcNow);
            await ApplySubmissionResultAsync(document, submission, consult, cancellationToken);
            return true;
        }

        if (SunatResponseCodes.IsInProcess(consult.ResponseCode, consult.Description)
            || consult.Status == SunatStatus.InProcess)
        {
            var submission = document.StartSubmission(clock.UtcNow);
            document.ApplySunatResult(
                submission,
                SunatStatus.InProcess,
                consult.ResponseCode ?? "0140",
                consult.Description,
                consult.Notes,
                consult.Ticket,
                null,
                clock.UtcNow);
            return true;
        }

        // 0127 / CommunicationError: only wait if a prior attempt already proved SUNAT has the ZIP.
        if ((SunatResponseCodes.IsCdrNotReady(consult.ResponseCode, consult.Description)
             || consult.Status == SunatStatus.CommunicationError)
            && LooksAlreadyReceivedBySunat(document))
        {
            var submission = document.StartSubmission(clock.UtcNow);
            document.ApplySunatResult(
                submission,
                SunatStatus.InProcess,
                consult.ResponseCode ?? "0127",
                consult.Description ?? "SUNAT aún procesa el comprobante; CDR no disponible.",
                consult.Notes,
                consult.Ticket,
                null,
                clock.UtcNow);
            return true;
        }

        logger.LogInformation(
            "Consult before retry for {Document} Code={Code} Status={Status}; will resend ZIP.",
            document.FullNumber,
            consult.ResponseCode,
            consult.Status);
        return false;
    }

    private static bool LooksAlreadyReceivedBySunat(ElectronicDocument document)
    {
        if (document.SunatStatus is SunatStatus.InProcess or SunatStatus.Pending
            or SunatStatus.Accepted or SunatStatus.AcceptedWithObservations)
        {
            return true;
        }

        foreach (var submission in document.Submissions)
        {
            if (SunatResponseCodes.IsInProcess(submission.ResponseCode, submission.Description)
                || SunatResponseCodes.IsAlreadyReported(submission.ResponseCode, submission.Description))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ApplySubmissionResultAsync(
        ElectronicDocument document,
        DocumentSubmission submission,
        SubmissionResult result,
        CancellationToken cancellationToken)
    {
        if (SunatResponseCodes.IsAlreadyReported(result.ResponseCode, result.Description))
        {
            document.ApplySunatResult(submission, SunatStatus.Accepted, result.ResponseCode, result.Description, result.Notes, result.Ticket, null, clock.UtcNow);
            return;
        }

        if (result.CdrZip is { Length: > 0 })
        {
            var parsed = cdrParser.Parse(result.CdrZip);
            var status = SunatResponseCodes.IsAlreadyReported(parsed.ResponseCode, parsed.Description)
                ? SunatStatus.Accepted
                : parsed.Status;
            document.ApplySunatResult(submission, status, parsed.ResponseCode, parsed.Description, parsed.Notes, result.Ticket, null, clock.UtcNow);
            if (document.GetFile(GeneratedFileKind.Cdr) is null)
            {
                await SaveRetryFileAsync(document, GeneratedFileKind.Zip, "R-" + document.XmlFileName + ".zip", "application/zip", result.CdrZip, cancellationToken);
                await SaveRetryFileAsync(document, GeneratedFileKind.Cdr, "R-" + document.XmlFileName + ".xml", "application/xml", parsed.OriginalXml, cancellationToken);
            }

            return;
        }

        var sunatStatus = result.Status;
        if (SunatResponseCodes.IsInProcess(result.ResponseCode, result.Description))
        {
            sunatStatus = SunatStatus.InProcess;
        }

        document.ApplySunatResult(submission, sunatStatus, result.ResponseCode, result.Description, result.Notes, result.Ticket, null, clock.UtcNow);
    }

    private async Task GeneratePdfAsync(ElectronicDocument document, CancellationToken cancellationToken)
    {
        var template = pdfTemplateResolver.Resolve(null);
        await documentPdfStore.SaveAsync(document, template, cancellationToken);
    }

    private async Task SaveRetryFileAsync(
        ElectronicDocument document,
        GeneratedFileKind kind,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var key = $"{document.IssuerRuc}/{document.DocumentTypeCode}/{document.Series}/{document.Number}/{kind}-{fileName}";
        var stored = await fileStorage.SaveAsync(key, fileName, contentType, content, cancellationToken);
        document.AddFile(GeneratedFile.Create(document.Id, kind, stored.Key, stored.FileName, stored.ContentType, clock.UtcNow));
    }
}
