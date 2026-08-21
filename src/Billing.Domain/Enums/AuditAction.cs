namespace Billing.Domain.Enums;

public enum AuditAction
{
    DocumentCreated = 0,
    XmlGenerated = 1,
    DocumentSigned = 2,
    SubmissionStarted = 3,
    SubmissionSent = 4,
    DocumentAccepted = 5,
    DocumentObserved = 6,
    DocumentRejected = 7,
    SubmissionRetried = 8,
    DocumentCancelled = 9,
    IssuerUpdated = 10,
    SeriesCreated = 11,
    SunatConsulted = 12,
    VoidSubmitted = 13
}
