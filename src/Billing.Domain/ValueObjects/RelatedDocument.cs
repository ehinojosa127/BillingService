using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public sealed record RelatedDocument
{
    public DocumentType DocumentType { get; }
    public string Series { get; }
    public int Number { get; }
    public NoteReasonCode Reason { get; }
    public string ReasonDescription { get; }

    public RelatedDocument(
        DocumentType documentType,
        string series,
        int number,
        NoteReasonCode reason,
        string? reasonDescription = null)
    {
        if (!documentType.IsInvoiceOrReceipt)
        {
            throw new BusinessRuleException("RELATED_DOCUMENT", "A note can only reference a factura or boleta.");
        }

        DocumentType = documentType;
        Series = new DocumentSeriesCode(series).Value;
        Number = new DocumentNumber(number).Value;
        Reason = reason;
        ReasonDescription = string.IsNullOrWhiteSpace(reasonDescription) ? reason.Name : reasonDescription.Trim();
    }

    public string FullNumber => DocumentNumberFormat.Combine(Series, Number);
}
