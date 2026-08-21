namespace Billing.Domain.Enums;

public enum SunatStatus
{
    NotSent = 0,
    Pending = 1,
    Accepted = 2,
    AcceptedWithObservations = 3,
    Rejected = 4,
    InProcess = 5,
    CommunicationError = 6
}
