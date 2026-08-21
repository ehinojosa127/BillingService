using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Exceptions;

namespace Billing.Domain.Services;

public static class DocumentStatusMachine
{
    private static readonly Dictionary<DocumentStatus, HashSet<DocumentStatus>> Allowed = new()
    {
        [DocumentStatus.Draft] = [DocumentStatus.Generated, DocumentStatus.Cancelled, DocumentStatus.Failed],
        [DocumentStatus.Generated] = [DocumentStatus.Signed, DocumentStatus.Failed, DocumentStatus.Cancelled],
        [DocumentStatus.Signed] = [DocumentStatus.Sent, DocumentStatus.Failed, DocumentStatus.Cancelled],
        [DocumentStatus.Sent] = [DocumentStatus.Accepted, DocumentStatus.Observed, DocumentStatus.Rejected, DocumentStatus.Failed],
        [DocumentStatus.Failed] = [DocumentStatus.Generated, DocumentStatus.Signed, DocumentStatus.Sent, DocumentStatus.Cancelled],
        [DocumentStatus.Rejected] = [DocumentStatus.Cancelled, DocumentStatus.Sent],
        [DocumentStatus.Observed] = [DocumentStatus.Cancelled],
        [DocumentStatus.Accepted] = [DocumentStatus.Cancelled],
        [DocumentStatus.Cancelled] = []
    };

    public static bool CanTransition(DocumentStatus from, DocumentStatus to)
    {
        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static DocumentStatus Transition(DocumentStatus from, DocumentStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStatusTransitionException(from, to);
        }

        return to;
    }

    public static bool CanRetrySubmission(DocumentStatus status) =>
        status is DocumentStatus.Draft or DocumentStatus.Failed or DocumentStatus.Signed or DocumentStatus.Sent;

    /// <summary>
    /// Un rechazo tributario de SUNAT no es reintentable. Sí lo son fallos transitorios
    /// (red, timeout, SUNAT no disponible) o documentos que quedaron firmados/enviados sin CDR.
    /// </summary>
    public static bool CanRetrySubmission(ElectronicDocument document)
    {
        if (document.SunatStatus is SunatStatus.Rejected
            or SunatStatus.Accepted
            or SunatStatus.AcceptedWithObservations)
        {
            return false;
        }

        if (document.Status is DocumentStatus.Rejected or DocumentStatus.Accepted or DocumentStatus.Observed or DocumentStatus.Cancelled)
        {
            return false;
        }

        return CanRetrySubmission(document.Status)
               || document.SunatStatus is SunatStatus.CommunicationError or SunatStatus.Pending or SunatStatus.InProcess;
    }

    public static bool CanCancel(ElectronicDocument document)
    {
        if (document.Status is DocumentStatus.Cancelled or DocumentStatus.Rejected)
        {
            return false;
        }

        var last = document.Submissions.LastOrDefault();
        if (last?.ErrorKind == "VoidSummary"
            && last.Status is SunatStatus.Pending or SunatStatus.InProcess)
        {
            return false;
        }

        return true;
    }

    public static bool RequiresSunatVoid(ElectronicDocument document) =>
        document.Status is DocumentStatus.Accepted or DocumentStatus.Observed;

    public static bool CanConsult(ElectronicDocument document)
    {
        if (document.Status is DocumentStatus.Cancelled)
        {
            return false;
        }

        var last = document.Submissions.LastOrDefault();
        if (last?.ErrorKind == "VoidSummary"
            && last.Status is SunatStatus.Pending or SunatStatus.InProcess)
        {
            return true;
        }

        if (document.Status is DocumentStatus.Sent or DocumentStatus.Failed)
        {
            return true;
        }

        return document.SunatStatus is SunatStatus.Pending
            or SunatStatus.InProcess
            or SunatStatus.CommunicationError;
    }
}
