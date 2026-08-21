namespace Billing.Domain.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid? DocumentId { get; private set; }
    public string ResponsePayload { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRecord()
    {
    }

    public static IdempotencyRecord Create(
        string key,
        string requestHash,
        Guid? documentId,
        string responsePayload,
        int statusCode,
        DateTimeOffset createdAt)
    {
        return new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            RequestHash = requestHash,
            DocumentId = documentId,
            ResponsePayload = responsePayload,
            StatusCode = statusCode,
            CreatedAt = createdAt
        };
    }
}
