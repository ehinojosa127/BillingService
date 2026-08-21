using Billing.Domain.Enums;

namespace Billing.Domain.Exceptions;

public sealed class InvalidStatusTransitionException : DomainException
{
    public DocumentStatus From { get; }
    public DocumentStatus To { get; }

    public InvalidStatusTransitionException(DocumentStatus from, DocumentStatus to)
        : base($"Cannot transition document status from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
