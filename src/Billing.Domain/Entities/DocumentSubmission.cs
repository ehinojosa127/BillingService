using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class DocumentSubmission
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Attempt { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public SunatStatus Status { get; private set; }
    public string? Ticket { get; private set; }
    public string? ResponseCode { get; private set; }
    public string? Description { get; private set; }
    public string? Notes { get; private set; }
    public string? ErrorKind { get; private set; }

    private DocumentSubmission()
    {
    }

    public static DocumentSubmission Start(Guid documentId, int attempt, DateTimeOffset now)
    {
        return new DocumentSubmission
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            Attempt = attempt,
            StartedAt = now.ToUniversalTime(),
            Status = SunatStatus.Pending
        };
    }

    public void Complete(
        SunatStatus status,
        DateTimeOffset now,
        string? ticket,
        string? responseCode,
        string? description,
        string? notes,
        string? errorKind)
    {
        Status = status;
        CompletedAt = now.ToUniversalTime();
        Ticket = ticket;
        ResponseCode = responseCode;
        Description = description;
        Notes = notes;
        ErrorKind = errorKind;
    }
}
