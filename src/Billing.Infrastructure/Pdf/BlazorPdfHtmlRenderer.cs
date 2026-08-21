using Billing.Application.Pdf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Billing.Infrastructure.Pdf;

public sealed class BlazorPdfHtmlRenderer
{
    public async Task<string> RenderAsync(Type componentType, BillingDocumentPdfViewModel model)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var htmlRenderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Templates.Shared.DocumentPdfView.Model)] = model
            });
            var output = await htmlRenderer.RenderComponentAsync(componentType, parameters);
            return output.ToHtmlString();
        });
    }
}
