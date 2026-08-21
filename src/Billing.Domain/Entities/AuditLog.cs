using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? DocumentId { get; private set; }
    public AuditAction Action { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? ExternalSystem { get; private set; }
    public string? RequestedBy { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? Details { get; private set; }

    private AuditLog()
    {
    }

    public static AuditLog Create(
        AuditAction action,
        DateTimeOffset occurredAt,
        Guid? documentId = null,
        string? externalSystem = null,
        string? requestedBy = null,
        string? correlationId = null,
        string? details = null)
    {
        return new AuditLog
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            Action = action,
            OccurredAt = occurredAt,
            ExternalSystem = externalSystem,
            RequestedBy = requestedBy,
            CorrelationId = correlationId,
            Details = details
        };
    }
}
