using Billing.Domain.Entities;
using Billing.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Billing.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Issuer> Issuers => Set<Issuer>();
    public DbSet<DocumentSeries> DocumentSeries => Set<DocumentSeries>();
    public DbSet<ElectronicDocument> Documents => Set<ElectronicDocument>();
    public DbSet<DocumentItem> DocumentItems => Set<DocumentItem>();
    public DbSet<DocumentReference> DocumentReferences => Set<DocumentReference>();
    public DbSet<DocumentSubmission> DocumentSubmissions => Set<DocumentSubmission>();
    public DbSet<GeneratedFile> GeneratedFiles => Set<GeneratedFile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<PdfTemplate> PdfTemplates => Set<PdfTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var id = entity.FindProperty("Id");
            if (id?.ClrType == typeof(Guid))
            {
                id.ValueGenerated = ValueGenerated.Never;
            }
        }

        ApplySnakeCase(modelBuilder);
    }

    private static void ApplySnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrWhiteSpace(columnName) && columnName != ToSnakeCase(columnName))
                {
                    var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
                    if (storeObject is not null)
                    {
                        property.SetColumnName(ToSnakeCase(property.GetColumnName(storeObject.Value) ?? columnName));
                    }
                }
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && value[i - 1] != '_' && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(c));
            }
            else
            {
                chars.Add(c);
            }
        }

        return new string(chars.ToArray());
    }
}
