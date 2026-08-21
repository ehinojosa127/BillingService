using Billing.Domain.Catalogs;

namespace Billing.Domain.Entities;

public sealed class DocumentReference
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string RelatedDocumentTypeCode { get; private set; } = string.Empty;
    public string Series { get; private set; } = string.Empty;
    public int Number { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string ReasonDescription { get; private set; } = string.Empty;

    private DocumentReference()
    {
    }

    public static DocumentReference FromRelated(Guid documentId, ValueObjects.RelatedDocument related)
    {
        return new DocumentReference
        {
            Id = Guid.CreateVersion7(),
            DocumentId = documentId,
            RelatedDocumentTypeCode = related.DocumentType.Code,
            Series = related.Series,
            Number = related.Number,
            ReasonCode = related.Reason.Code,
            ReasonDescription = related.ReasonDescription
        };
    }

    public DocumentType RelatedDocumentType => DocumentType.FromCode(RelatedDocumentTypeCode);
    public string FullNumber => ValueObjects.DocumentNumberFormat.Combine(Series, Number);
}
