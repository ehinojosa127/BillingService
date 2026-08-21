using Billing.Application.Abstractions;
using Billing.Application.DTOs;
using Billing.Application.Exceptions;
using Billing.Application.Pdf;
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
            var submission = document.StartSubmission(clock.UtcNow);
            var simulation = BillingTestSimulation.Resolve(document.Observation, taxProfile.IsProductionEnvironment);
            if (simulation != BillingTestSimulationMode.None)
            {
                BillingTestSimulation.Apply(document, submission, simulation, clock.UtcNow);
            }
            else
            {
                var result = await documentProvider.SubmitAsync(document, signedXml, cancellationToken);
                if (SunatResponseCodes.IsAlreadyReported(result.ResponseCode, result.Description))
                {
                    document.ApplySunatResult(submission, SunatStatus.Accepted, result.ResponseCode, result.Description, result.Notes, result.Ticket, null, clock.UtcNow);
                }
                else if (result.CdrZip is { Length: > 0 })
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
                }
                else
                {
                    document.ApplySunatResult(submission, result.Status, result.ResponseCode, result.Description, result.Notes, result.Ticket, null, clock.UtcNow);
                }
            }

            if (document.GetFile(GeneratedFileKind.Pdf) is null)
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
            logger.LogWarning(ex, "Retry submission failed for {DocumentId}", document.Id);
            document.MarkFailed(ex.GetType().Name, ex.Message, clock.UtcNow);
        }

        await documentRepository.UpdateAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToResult(document);
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
