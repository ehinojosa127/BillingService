using Billing.Shared;

namespace Billing.WebApi.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers[BillingHeaders.CorrelationId].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.CreateVersion7().ToString("N");
        }

        context.Items[BillingHeaders.CorrelationId] = correlationId;
        context.Response.Headers[BillingHeaders.CorrelationId] = correlationId;
        using (context.RequestServices.GetRequiredService<ILoggerFactory>()
                   .CreateLogger("Correlation")
                   .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
