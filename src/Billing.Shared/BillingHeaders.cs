namespace Billing.Shared;

public static class BillingHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string ApiKey = "X-Api-Key";
}
