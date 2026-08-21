using Billing.Application.Abstractions;
using Billing.Domain.Catalogs;
using Billing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure;

public sealed class OptionsIssuerTaxProfile(IOptions<SunatOptions> options) : IIssuerTaxProfile
{
    public TaxRegime Regime => TaxRegime.FromCode(options.Value.TaxRegime);

    public TaxpayerType TaxpayerType => TaxpayerType.FromCode(options.Value.TaxpayerType);

    public bool IsProductionEnvironment => options.Value.IsProduction;
}
