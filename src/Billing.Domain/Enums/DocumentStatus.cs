namespace Billing.Domain.Enums;

public enum DocumentStatus
{
    Draft = 0,
    Generated = 1,
    Signed = 2,
    Sent = 3,
    Accepted = 4,
    Observed = 5,
    Rejected = 6,
    Failed = 7,
    Cancelled = 8
}
