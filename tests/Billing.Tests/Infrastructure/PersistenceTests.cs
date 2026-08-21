using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.ValueObjects;
using Billing.Infrastructure.Configuration;
using Billing.Infrastructure.Persistence;
using Billing.Infrastructure.Persistence.Repositories;
using Billing.Infrastructure.Time;
using Billing.Tests.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Tests.Infrastructure;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        EnvFileLoader.LoadDefaultLocations();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BILLING_TEST_POSTGRES"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DB_HOST")))
        {
            Skip = "Set DB_* environment variables or BILLING_TEST_POSTGRES=1 to run persistence tests.";
        }
    }
}

public sealed class PersistenceTests
{
    [PostgresFact]
    public async Task Concurrent_allocations_do_not_duplicate_correlatives()
    {
        await using var db = CreateContext();
        var clock = new SystemClock();
        var seriesCode = "F" + Random.Shared.Next(100, 999).ToString();
        var numbers = new int[10];
        await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            await using var local = CreateContext();
            var repo = new DocumentSeriesRepository(local, clock);
            numbers[i] = await repo.AllocateNextNumberAsync(DocumentType.Invoice, seriesCode, CancellationToken.None);
        }));

        Assert.Equal(10, numbers.Distinct().Count());
        Assert.Equal(55, numbers.Sum());
    }

    [PostgresFact]
    public async Task Persists_issuer_series_document_and_external_reference()
    {
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var document = DocumentFactory.Invoice(number: Random.Shared.Next(10000, 99999));
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var loaded = await db.Documents
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == document.Id);

        Assert.Equal("test-erp", loaded.ExternalSystem);
        Assert.Equal("order", loaded.ExternalEntity);
        Assert.Equal("42", loaded.ExternalId);
        Assert.Equal("ORD-1", loaded.ExternalReference);
        Assert.Single(loaded.Items);
        Assert.Equal(DocumentStatus.Draft, loaded.Status);

        loaded.AddFile(GeneratedFile.Create(
            loaded.Id,
            GeneratedFileKind.Pdf,
            "test/pdf",
            "test.pdf",
            "application/pdf",
            DateTimeOffset.UtcNow));
        var documents = new DocumentRepository(db);
        await documents.UpdateAsync(loaded, CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.Contains(await db.GeneratedFiles.ToListAsync(), x => x.DocumentId == loaded.Id);

        var templates = new PdfTemplateRepository(db);
        var existing = await templates.ListAsync(CancellationToken.None);
        if (existing.Count == 0)
        {
            await templates.AddAsync(PdfTemplate.Create(PdfTemplate.DefaultCode, "Default", true, DateTimeOffset.UtcNow), CancellationToken.None);
            await templates.AddAsync(PdfTemplate.Create(PdfTemplate.CustomCode, "Custom", false, DateTimeOffset.UtcNow), CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var listed = await templates.ListAsync(CancellationToken.None);
        Assert.Contains(listed, x => x.Code == PdfTemplate.DefaultCode);

        var record = IdempotencyRecord.Create(
            "order:42:invoice:" + Guid.NewGuid().ToString("N"),
            "abc",
            document.Id,
            "{}",
            201,
            DateTimeOffset.UtcNow);
        db.IdempotencyRecords.Add(record);
        await db.SaveChangesAsync();
        Assert.NotNull(await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.Key == record.Key));

        await transaction.RollbackAsync();
    }

    [PostgresFact]
    public async Task Unique_document_number_is_enforced()
    {
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var number = Random.Shared.Next(20000, 30000);
        db.Documents.Add(DocumentFactory.Invoice(number: number));
        await db.SaveChangesAsync();
        db.Documents.Add(DocumentFactory.Invoice(number: number));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    private static BillingDbContext CreateContext()
    {
        EnvFileLoader.LoadDefaultLocations();
        var host = Environment.GetEnvironmentVariable("DB_HOST")
                   ?? throw new InvalidOperationException("DB_HOST is required.");
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_DATABASE")
                       ?? throw new InvalidOperationException("DB_DATABASE is required.");
        var username = Environment.GetEnvironmentVariable("DB_USERNAME")
                       ?? throw new InvalidOperationException("DB_USERNAME is required.");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql($"Host={host};Port={port};Database={database};Username={username};Password={password}")
            .Options;
        return new BillingDbContext(options);
    }
}
