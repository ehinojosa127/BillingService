using Billing.Domain.Catalogs;
using Billing.Domain.Exceptions;

namespace Billing.Domain.ValueObjects;

public sealed record ShippingGuideInfo
{
    public TransferReasonCode TransferReason { get; }
    public TransportModeCode TransportMode { get; }
    public DateOnly TransferStartDate { get; }
    public decimal GrossWeightKg { get; }
    public int PackageCount { get; }
    public Address Origin { get; }
    public Address Destination { get; }
    public string? CarrierRuc { get; }
    public string? CarrierName { get; }
    public string? VehiclePlate { get; }
    public string? DriverLicense { get; }
    public string? DriverDocumentNumber { get; }
    public IdentityDocumentType? DriverDocumentType { get; }
    public string? Observation { get; }

    public ShippingGuideInfo(
        TransferReasonCode transferReason,
        TransportModeCode transportMode,
        DateOnly transferStartDate,
        decimal grossWeightKg,
        int packageCount,
        Address origin,
        Address destination,
        string? carrierRuc = null,
        string? carrierName = null,
        string? vehiclePlate = null,
        string? driverLicense = null,
        string? driverDocumentNumber = null,
        IdentityDocumentType? driverDocumentType = null,
        string? observation = null)
    {
        if (grossWeightKg <= 0)
        {
            throw new BusinessRuleException("GUIDE", "Gross weight must be greater than zero.");
        }

        if (packageCount < 1)
        {
            throw new BusinessRuleException("GUIDE", "Package count must be at least 1.");
        }

        if (transportMode == TransportModeCode.Public)
        {
            if (string.IsNullOrWhiteSpace(carrierRuc) || !Ruc.IsValid(carrierRuc))
            {
                throw new BusinessRuleException("GUIDE", "Public transport requires a valid carrier RUC.");
            }

            if (string.IsNullOrWhiteSpace(carrierName))
            {
                throw new BusinessRuleException("GUIDE", "Public transport requires the carrier legal name.");
            }
        }

        if (transportMode == TransportModeCode.Private && string.IsNullOrWhiteSpace(vehiclePlate))
        {
            throw new BusinessRuleException("GUIDE", "Private transport requires a vehicle plate.");
        }

        TransferReason = transferReason;
        TransportMode = transportMode;
        TransferStartDate = transferStartDate;
        GrossWeightKg = Money.Round(grossWeightKg);
        PackageCount = packageCount;
        Origin = origin;
        Destination = destination;
        CarrierRuc = string.IsNullOrWhiteSpace(carrierRuc) ? null : carrierRuc.Trim();
        CarrierName = string.IsNullOrWhiteSpace(carrierName) ? null : carrierName.Trim();
        VehiclePlate = string.IsNullOrWhiteSpace(vehiclePlate) ? null : vehiclePlate.Trim().ToUpperInvariant();
        DriverLicense = string.IsNullOrWhiteSpace(driverLicense) ? null : driverLicense.Trim();
        DriverDocumentNumber = string.IsNullOrWhiteSpace(driverDocumentNumber) ? null : driverDocumentNumber.Trim();
        DriverDocumentType = driverDocumentType;
        Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
    }
}
