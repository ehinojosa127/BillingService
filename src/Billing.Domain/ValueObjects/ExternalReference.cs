namespace Billing.Domain.ValueObjects;

/// <summary>
/// Referencia débil hacia un sistema externo. Son valores simples, nunca FK.
/// </summary>
public sealed record ExternalReference(
    string? System,
    string? Reference,
    string? Entity = null,
    string? Id = null)
{
    public bool HasValue =>
        !string.IsNullOrWhiteSpace(System)
        || !string.IsNullOrWhiteSpace(Reference)
        || !string.IsNullOrWhiteSpace(Entity)
        || !string.IsNullOrWhiteSpace(Id);
}
