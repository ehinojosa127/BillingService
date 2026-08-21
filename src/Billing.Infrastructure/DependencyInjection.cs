using Billing.Application.Abstractions;
using Billing.Infrastructure.Certificates;
using Billing.Infrastructure.Configuration;
using Billing.Infrastructure.Pdf;
using Billing.Infrastructure.Persistence;
using Billing.Infrastructure.Persistence.Repositories;
using Billing.Infrastructure.Qr;
using Billing.Infrastructure.Signing;
using Billing.Infrastructure.Storage;
using Billing.Infrastructure.Sunat;
using Billing.Infrastructure.Time;
using Billing.Infrastructure.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AppContext.SetSwitch("Switch.System.Security.Cryptography.Xml.UseInsecureHashAlgorithms", true);

        BindOptions(services, configuration);

        services.AddDbContext<BillingDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(db.ToConnectionString());
        });

        services.AddScoped<IIssuerRepository, IssuerRepository>();
        services.AddScoped<IDocumentSeriesRepository, DocumentSeriesRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPdfTemplateRepository, PdfTemplateRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IXmlDocumentGenerator, UblXmlDocumentGenerator>();
        services.AddSingleton<IVoidedDocumentsXmlGenerator, VoidedDocumentsXmlGenerator>();
        services.AddSingleton<ICdrParser, CdrParser>();
        services.AddSingleton<IQrCodeGenerator, SunatQrCodeGenerator>();
        services.AddSingleton<IPdfTemplateComponentResolver, PdfTemplateComponentResolver>();
        services.AddSingleton<BlazorPdfHtmlRenderer>();
        services.AddSingleton<ChromiumHtmlToPdfRenderer>();
        services.AddSingleton<IPdfGenerator, DocumentPdfGenerator>();
        services.AddScoped<IPdfBrandingProvider, PdfBrandingProvider>();
        services.AddScoped<ICertificateProvider, FileCertificateProvider>();
        services.AddScoped<IXmlSigner, XmlDsigSigner>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IElectronicDocumentProvider, SunatDirectElectronicDocumentProvider>();
        services.AddSingleton<IIssuerTaxProfile, OptionsIssuerTaxProfile>();

        services.AddHttpClient(SunatDirectElectronicDocumentProvider.BillClientName, (sp, client) =>
        {
            var sunat = sp.GetRequiredService<IOptions<SunatOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(sunat.TimeoutSeconds, 30));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BillingService/1.0");
        }).AddStandardResilienceHandler(options => ConfigureTransportRetry(options, 2));

        services.AddHttpClient(SunatDirectElectronicDocumentProvider.GreClientName, (sp, client) =>
        {
            var sunat = sp.GetRequiredService<IOptions<SunatOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(sunat.TimeoutSeconds, 30));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BillingService/1.0");
        }).AddStandardResilienceHandler(options => ConfigureTransportRetry(options, 3));

        services.AddHealthChecks()
            .AddCheck("Billing.WebApi", () => HealthCheckResult.Healthy())
            .AddDbContextCheck<BillingDbContext>("PostgreSQL");
        return services;
    }

    private static void BindOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(options =>
        {
            configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
            options.Host = First(configuration, "DB_HOST", options.Host);
            options.Port = int.TryParse(configuration["DB_PORT"], out var port) ? port : options.Port;
            options.Name = First(configuration, "DB_DATABASE", options.Name);
            options.Username = First(configuration, "DB_USERNAME", options.Username);
            options.Password = configuration["DB_PASSWORD"] ?? options.Password;
        });

        services.Configure<SunatOptions>(options =>
        {
            configuration.GetSection(SunatOptions.SectionName).Bind(options);
            options.Environment = First(configuration, "SUNAT_ENVIRONMENT", options.Environment);
            options.Ruc = First(configuration, "SUNAT_RUC", options.Ruc);
            options.SolUsername = First(configuration, "SUNAT_SOL_USERNAME", options.SolUsername);
            options.SolPassword = configuration["SUNAT_SOL_PASSWORD"] ?? options.SolPassword;
            options.CertificatePath = First(configuration, "SUNAT_CERTIFICATE_PATH", options.CertificatePath);
            options.CertificatePassword = configuration["SUNAT_CERTIFICATE_PASSWORD"] ?? options.CertificatePassword;
            options.GreClientId = First(configuration, "SUNAT_GRE_CLIENT_ID", options.GreClientId);
            options.GreClientSecret = configuration["SUNAT_GRE_CLIENT_SECRET"] ?? options.GreClientSecret;
            options.TaxRegime = First(configuration, "SUNAT_TAX_REGIME", options.TaxRegime);
            options.TaxpayerType = First(configuration, "SUNAT_TAXPAYER_TYPE", options.TaxpayerType);
            ApplyEnvironmentDefaults(options);
        });

        services.Configure<StorageOptions>(options =>
        {
            configuration.GetSection(StorageOptions.SectionName).Bind(options);
            options.Root = First(configuration, "STORAGE_ROOT", options.Root);
        });

        services.Configure<PdfBrandingOptions>(options =>
        {
            configuration.GetSection(PdfBrandingOptions.SectionName).Bind(options);
            options.CompanyName = First(configuration, "PDF_COMPANY_NAME", options.CompanyName);
            options.PrimaryColor = First(configuration, "PDF_PRIMARY_COLOR", options.PrimaryColor);
            options.LogoPath = First(configuration, "PDF_LOGO_PATH", options.LogoPath);
        });
    }

    internal static void ApplyEnvironmentDefaults(SunatOptions options)
    {
        if (options.IsProduction)
        {
            options.BillServiceUrl = FirstValue(options.BillServiceUrl, "https://e-factura.sunat.gob.pe/ol-ti-itcpfegem/billService");
            options.ConsultServiceUrl = FirstValue(options.ConsultServiceUrl, "https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService");
            options.GreTokenUrl = FirstValue(options.GreTokenUrl, "https://api-seguridad.sunat.gob.pe/v1/clientessol/{client_id}/oauth2/token/");
            options.GreApiUrl = FirstValue(options.GreApiUrl, "https://api-cpe.sunat.gob.pe/v1");
            return;
        }

        options.BillServiceUrl = FirstValue(options.BillServiceUrl, "https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService");
        options.ConsultServiceUrl = FirstValue(options.ConsultServiceUrl, "https://e-beta.sunat.gob.pe/ol-it-wsconscpegem-beta/billConsultService");
        options.GreTokenUrl = FirstValue(options.GreTokenUrl, "https://gre-test.nubefact.com/v1/clientessol/{client_id}/oauth2/token/");
        options.GreApiUrl = FirstValue(options.GreApiUrl, "https://gre-test.nubefact.com/v1");
    }

    private static void ConfigureTransportRetry(HttpStandardResilienceOptions options, int attempts)
    {
        options.Retry.MaxRetryAttempts = attempts;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Exception is HttpRequestException or IOException or TaskCanceledException);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    }

    private static string First(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string FirstValue(string current, string fallback) =>
        string.IsNullOrWhiteSpace(current) ? fallback : current;
}
