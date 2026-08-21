using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Entities;

public sealed class DocumentSeries
{
    public Guid Id { get; private set; }
    public string DocumentTypeCode { get; private set; } = string.Empty;
    public string Series { get; private set; } = string.Empty;
    public int LastNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DocumentSeries()
    {
    }

    public static DocumentSeries Create(DocumentType documentType, string series, DateTimeOffset now, int lastNumber = 0)
    {
        var code = new DocumentSeriesCode(series);
        if (!documentType.IsNote)
        {
            code.EnsureCompatibleWith(documentType);
        }
        else if (code.Prefix is not ('F' or 'B'))
        {
            throw new BusinessRuleException("SERIES", "Note series must start with F or B depending on the related document.");
        }

        if (lastNumber < 0)
        {
            throw new BusinessRuleException("SERIES", "Last number cannot be negative.");
        }

        return new DocumentSeries
        {
            Id = Guid.CreateVersion7(),
            DocumentTypeCode = documentType.Code,
            Series = code.Value,
            LastNumber = lastNumber,
            IsActive = true,
            CreatedAt = now
        };
    }

    public DocumentType Type => DocumentType.FromCode(DocumentTypeCode);

    public int NextNumber()
    {
        if (!IsActive)
        {
            throw new BusinessRuleException("SERIES", $"Series '{Series}' is inactive.");
        }

        if (LastNumber >= 99_999_999)
        {
            throw new BusinessRuleException("SERIES", $"Series '{Series}' has reached the maximum correlative.");
        }

        LastNumber += 1;
        return LastNumber;
    }
}
