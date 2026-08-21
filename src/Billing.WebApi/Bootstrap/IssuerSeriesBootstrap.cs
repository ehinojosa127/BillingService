using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Application.Exceptions;
using Billing.Domain.Catalogs;
using MediatR;

namespace Billing.WebApi.Bootstrap;

/// <summary>
/// Crea emisor + series mínimas si faltan (idempotente).
/// Usa valores por defecto seguros para desarrollo/Docker si faltan variables.
/// </summary>
public static class IssuerSeriesBootstrap
{
    public const string DefaultRuc = "20000000001";
    public const string DefaultLegalName = "EMPRESA DEMO S.A.C.";
    public const string DefaultAddressLine = "AV. DEMO 123";
    public const string DefaultUbigeo = "150101";
    public const string DefaultDepartment = "LIMA";
    public const string DefaultProvince = "LIMA";
    public const string DefaultDistrict = "LIMA";

    public static async Task RunAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunCoreAsync(services, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Issuer/series bootstrap failed.");
            throw;
        }
    }

    private static async Task RunCoreAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var configuredRuc = FirstEnv("ISSUER_RUC", "SUNAT_RUC");
        var ruc = configuredRuc ?? DefaultRuc;
        var usedDefaults = configuredRuc is null;

        if (!Domain.ValueObjects.Ruc.IsValid(ruc))
        {
            logger.LogWarning("Configured RUC '{Ruc}' is invalid; falling back to demo RUC {Default}.", ruc, DefaultRuc);
            ruc = DefaultRuc;
            usedDefaults = true;
        }

        await using var scope = services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var issuers = scope.ServiceProvider.GetRequiredService<IIssuerRepository>();
        var series = scope.ServiceProvider.GetRequiredService<IDocumentSeriesRepository>();

        var existing = await issuers.GetAsync(cancellationToken);
        if (existing is null)
        {
            var legalName = FirstEnv("ISSUER_LEGAL_NAME") ?? DefaultLegalName;
            var tradeName = FirstEnv("ISSUER_TRADE_NAME") ?? legalName;
            var addressLine = FirstEnv("ISSUER_ADDRESS_LINE") ?? DefaultAddressLine;
            var ubigeo = FirstEnv("ISSUER_UBIGEO") ?? DefaultUbigeo;
            var department = FirstEnv("ISSUER_DEPARTMENT") ?? DefaultDepartment;
            var province = FirstEnv("ISSUER_PROVINCE") ?? DefaultProvince;
            var district = FirstEnv("ISSUER_DISTRICT") ?? DefaultDistrict;

            await mediator.Send(new UpsertIssuerCommand
            {
                Ruc = ruc.Trim(),
                LegalName = legalName,
                TradeName = tradeName,
                AddressLine = addressLine,
                Ubigeo = ubigeo,
                Department = department,
                Province = province,
                District = district,
                CountryCode = "PE",
                EstablishmentCode = FirstEnv("ISSUER_ESTABLISHMENT_CODE") ?? "0000",
                Email = FirstEnv("ISSUER_EMAIL"),
                Phone = FirstEnv("ISSUER_PHONE")
            }, cancellationToken);

            logger.LogInformation(
                usedDefaults
                    ? "Issuer bootstrapped with DEMO defaults (RUC {Ruc}). Replace SUNAT_RUC / ISSUER_* for real SUNAT use."
                    : "Issuer bootstrapped for RUC {Ruc}.",
                ruc);
        }
        else
        {
            logger.LogInformation("Issuer already configured ({Ruc}).", existing.Ruc);
        }

        await EnsureSeriesAsync(mediator, series, DocumentType.Receipt, "B001", logger, cancellationToken);
        await EnsureSeriesAsync(mediator, series, DocumentType.Invoice, "F001", logger, cancellationToken);
    }

    private static async Task EnsureSeriesAsync(
        IMediator mediator,
        IDocumentSeriesRepository series,
        DocumentType type,
        string code,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await series.GetAsync(type, code, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        try
        {
            await mediator.Send(new CreateSeriesCommand(type.Code, code), cancellationToken);
            logger.LogInformation("Series {Type}/{Code} created.", type.Code, code);
        }
        catch (ConflictException)
        {
            logger.LogInformation("Series {Type}/{Code} already exists.", type.Code, code);
        }
    }

    private static string? FirstEnv(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            // Docker env_file define claves vacías; tratarlas como no configuradas.
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
