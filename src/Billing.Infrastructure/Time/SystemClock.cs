using Billing.Application.Abstractions;

namespace Billing.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Lima =
        TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset LimaNow => TimeZoneInfo.ConvertTime(UtcNow, Lima);
}
