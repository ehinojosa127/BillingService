using Billing.WebApi.Bootstrap;

namespace Billing.WebApi.Bootstrap;

/// <summary>
/// Garantiza emisor + series al iniciar el proceso HTTP (no depende de un paso aparte en entrypoint).
/// </summary>
public sealed class IssuerBootstrapHostedService(
    IServiceProvider services,
    ILogger<IssuerBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await IssuerSeriesBootstrap.RunAsync(services, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Issuer bootstrap on startup failed. Emisión fallará hasta configurar el emisor.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
