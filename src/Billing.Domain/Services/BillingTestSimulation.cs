using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Domain.Services;

public enum BillingTestSimulationMode
{
    None,
    Rejected,
    Pending,
}

/// <summary>
/// Códigos en observaciones para simular respuestas SUNAT en entornos no productivos.
/// </summary>
public static class BillingTestSimulation
{
    private static readonly string[] RejectedTokens =
    [
        "#TEST:REJECTED",
        "#SUNAT:RECHAZADO",
        "TEST-REJECTED",
    ];

    private static readonly string[] PendingTokens =
    [
        "#TEST:PENDING",
        "#SUNAT:PENDIENTE",
        "TEST-PENDING",
    ];

    public static BillingTestSimulationMode Resolve(string? observation, bool isProductionEnvironment)
    {
        if (isProductionEnvironment || string.IsNullOrWhiteSpace(observation))
        {
            return BillingTestSimulationMode.None;
        }

        if (ContainsToken(observation, RejectedTokens))
        {
            return BillingTestSimulationMode.Rejected;
        }

        if (ContainsToken(observation, PendingTokens))
        {
            return BillingTestSimulationMode.Pending;
        }

        return BillingTestSimulationMode.None;
    }

    public static string? SanitizeObservation(string? observation, bool isProductionEnvironment)
    {
        if (isProductionEnvironment || string.IsNullOrWhiteSpace(observation))
        {
            return observation;
        }

        var sanitized = observation;
        foreach (var token in RejectedTokens.Concat(PendingTokens))
        {
            sanitized = sanitized.Replace(token, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        sanitized = sanitized.Trim();
        return sanitized.Length == 0 ? null : sanitized;
    }

    public static void Apply(
        ElectronicDocument document,
        DocumentSubmission submission,
        BillingTestSimulationMode mode,
        DateTimeOffset now)
    {
        switch (mode)
        {
            case BillingTestSimulationMode.Rejected:
                document.ApplySunatResult(
                    submission,
                    SunatStatus.Rejected,
                    "TEST-REJ",
                    "Simulación de prueba: comprobante rechazado (código en observaciones del pedido).",
                    null,
                    null,
                    "TestSimulation",
                    now);
                break;
            case BillingTestSimulationMode.Pending:
                document.ApplySunatResult(
                    submission,
                    SunatStatus.InProcess,
                    "98",
                    "Simulación de prueba: comprobante pendiente en SUNAT (código en observaciones del pedido).",
                    null,
                    $"TEST-PENDING-{document.Id:N}",
                    "TestSimulation",
                    now);
                break;
            default:
                throw new InvalidOperationException($"Unsupported test simulation mode '{mode}'.");
        }
    }

    private static bool ContainsToken(string observation, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (observation.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
