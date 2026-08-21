using Billing.Application.Abstractions;
using Billing.Application.Behaviors;
using Billing.Application.Pdf;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<IPdfTemplateResolver, PdfTemplateResolver>();
        services.AddScoped<DocumentPdfStore>();
        return services;
    }
}
