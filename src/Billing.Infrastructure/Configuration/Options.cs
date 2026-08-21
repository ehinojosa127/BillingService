namespace Billing.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string ToConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException(
                "Database configuration is missing. Set DB_HOST, DB_PORT, DB_DATABASE, DB_USERNAME and DB_PASSWORD.");
        }

        return $"Host={Host};Port={Port};Database={Name};Username={Username};Password={Password}";
    }
}

public sealed class SunatOptions
{
    public const string SectionName = "Sunat";

    public string Environment { get; set; } = "beta";
    public string TaxRegime { get; set; } = "general";
    public string TaxpayerType { get; set; } = "legal";
    public string Ruc { get; set; } = string.Empty;
    public string SolUsername { get; set; } = string.Empty;
    public string SolPassword { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    public string BillServiceUrl { get; set; } = string.Empty;
    public string ConsultServiceUrl { get; set; } = string.Empty;
    public string GreTokenUrl { get; set; } = string.Empty;
    public string GreApiUrl { get; set; } = string.Empty;
    public string GreClientId { get; set; } = string.Empty;
    public string GreClientSecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;

    public bool IsProduction => string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase);

    public string SolUser => $"{Ruc}{SolUsername}";
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string Root { get; set; } = "./storage";
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string ApiKey { get; set; } = string.Empty;
}
