using Billing.Infrastructure.Configuration;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Billing.Infrastructure.Persistence;

public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        EnvFileLoader.LoadDefaultLocations();
        var host = Required("DB_HOST");
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Required("DB_DATABASE");
        var username = Required("DB_USERNAME");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql($"Host={host};Port={port};Database={database};Username={username};Password={password}")
            .Options;
        return new BillingDbContext(options);
    }

    private static string Required(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Missing {key}. Copy .env.example to .env or set environment variables.");
}
