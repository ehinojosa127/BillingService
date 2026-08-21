namespace Billing.Tests.Infrastructure;

public sealed class SunatBetaFactAttribute : FactAttribute
{
    public SunatBetaFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SUNAT_BETA_TESTS"), "1", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set SUNAT_BETA_TESTS=1 plus certificate and SOL credentials to run optional SUNAT beta tests.";
        }
    }
}

public sealed class SunatBetaTests
{
    [SunatBetaFact]
    public void Credentials_are_present()
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUNAT_CERTIFICATE_PATH")));
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUNAT_RUC")));
    }
}
