using Billing.Domain.Exceptions;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Entities;

public sealed class Issuer
{
    public Guid Id { get; private set; }
    public string Ruc { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public Address Address { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string EstablishmentCode { get; private set; } = "0000";
    public DateTimeOffset UpdatedAt { get; private set; }

    private Issuer()
    {
    }

    public static Issuer Create(
        string ruc,
        string legalName,
        string tradeName,
        Address address,
        string? email,
        string? phone,
        string establishmentCode,
        DateTimeOffset now)
    {
        var issuer = new Issuer { Id = Guid.CreateVersion7() };
        issuer.Update(ruc, legalName, tradeName, address, email, phone, establishmentCode, now);
        return issuer;
    }

    public void Update(
        string ruc,
        string legalName,
        string tradeName,
        Address address,
        string? email,
        string? phone,
        string establishmentCode,
        DateTimeOffset now)
    {
        var parsedRuc = new Ruc(ruc);
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new BusinessRuleException("ISSUER", "Legal name is required.");
        }

        Ruc = parsedRuc.Value;
        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? LegalName : tradeName.Trim();
        Address = address;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        EstablishmentCode = string.IsNullOrWhiteSpace(establishmentCode) ? "0000" : establishmentCode.Trim();
        UpdatedAt = now;
    }
}
