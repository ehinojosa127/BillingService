using Billing.Domain.Catalogs;

namespace Billing.Application.Abstractions;

public interface IIssuerTaxProfile
{
    TaxRegime Regime { get; }

    TaxpayerType TaxpayerType { get; }

    bool IsProductionEnvironment { get; }
}
