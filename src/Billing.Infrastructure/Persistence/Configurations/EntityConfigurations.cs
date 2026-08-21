using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Configurations;

public sealed class IssuerConfiguration : IEntityTypeConfiguration<Issuer>
{
    public void Configure(EntityTypeBuilder<Issuer> builder)
    {
        builder.ToTable("issuers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Ruc).HasMaxLength(11).IsRequired();
        builder.HasIndex(x => x.Ruc).IsUnique();
        builder.Property(x => x.LegalName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TradeName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.EstablishmentCode).HasMaxLength(4).IsRequired();
        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(p => p.Line).HasColumnName("address_line").HasMaxLength(500).IsRequired();
            address.Property(p => p.Ubigeo).HasColumnName("ubigeo").HasMaxLength(6).IsRequired();
            address.Property(p => p.Department).HasColumnName("department").HasMaxLength(100).IsRequired();
            address.Property(p => p.Province).HasColumnName("province").HasMaxLength(100).IsRequired();
            address.Property(p => p.District).HasColumnName("district").HasMaxLength(100).IsRequired();
            address.Property(p => p.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
            address.Property(p => p.Urbanization).HasColumnName("urbanization").HasMaxLength(100);
        });
    }
}

public sealed class DocumentSeriesConfiguration : IEntityTypeConfiguration<DocumentSeries>
{
    public void Configure(EntityTypeBuilder<DocumentSeries> builder)
    {
        builder.ToTable("document_series");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentTypeCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Series).HasMaxLength(4).IsRequired();
        builder.HasIndex(x => new { x.DocumentTypeCode, x.Series }).IsUnique();
    }
}

public sealed class ElectronicDocumentConfiguration : IEntityTypeConfiguration<ElectronicDocument>
{
    public void Configure(EntityTypeBuilder<ElectronicDocument> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentTypeCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Series).HasMaxLength(4).IsRequired();
        builder.HasIndex(x => new { x.DocumentTypeCode, x.Series, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.ExternalSystem, x.ExternalReference });
        builder.HasIndex(x => new { x.ExternalSystem, x.ExternalEntity, x.ExternalId });
        builder.Property(x => x.ExternalSystem).HasMaxLength(100);
        builder.Property(x => x.ExternalEntity).HasMaxLength(100);
        builder.Property(x => x.ExternalId).HasMaxLength(80);
        builder.Property(x => x.ExternalReference).HasMaxLength(100);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.IssuerRuc).HasMaxLength(11).IsRequired();
        builder.Property(x => x.RecipientIdentityNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TaxableAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExemptAmount).HasPrecision(18, 2);
        builder.Property(x => x.UnaffectedAmount).HasPrecision(18, 2);
        builder.Property(x => x.FreeAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExportAmount).HasPrecision(18, 2);
        builder.Property(x => x.IgvAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineExtensionAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxInclusiveAmount).HasPrecision(18, 2);
        builder.Property(x => x.PayableAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.ChargeAmount).HasPrecision(18, 2);
        builder.Property(x => x.GrossWeightKg).HasPrecision(18, 3);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.References).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Submissions).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.References).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Submissions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class DocumentItemConfiguration : IEntityTypeConfiguration<DocumentItem>
{
    public void Configure(EntityTypeBuilder<DocumentItem> builder)
    {
        builder.ToTable("document_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UnitCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.UnitValue).HasPrecision(18, 6);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 6);
        builder.Property(x => x.Discount).HasPrecision(18, 2);
        builder.Property(x => x.TaxableAmount).HasPrecision(18, 2);
        builder.Property(x => x.IgvAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
    }
}

public sealed class DocumentReferenceConfiguration : IEntityTypeConfiguration<DocumentReference>
{
    public void Configure(EntityTypeBuilder<DocumentReference> builder)
    {
        builder.ToTable("document_references");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelatedDocumentTypeCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Series).HasMaxLength(4).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.ReasonDescription).HasMaxLength(250).IsRequired();
    }
}

public sealed class DocumentSubmissionConfiguration : IEntityTypeConfiguration<DocumentSubmission>
{
    public void Configure(EntityTypeBuilder<DocumentSubmission> builder)
    {
        builder.ToTable("document_submissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Ticket).HasMaxLength(100);
        builder.Property(x => x.ResponseCode).HasMaxLength(10);
        builder.Property(x => x.ErrorKind).HasMaxLength(80);
    }
}

public sealed class GeneratedFileConfiguration : IEntityTypeConfiguration<GeneratedFile>
{
    public void Configure(EntityTypeBuilder<GeneratedFile> builder)
    {
        builder.ToTable("generated_files");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PdfTemplate).HasMaxLength(16);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.DocumentId);
        builder.HasIndex(x => x.OccurredAt);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.ExternalSystem).HasMaxLength(100);
        builder.Property(x => x.RequestedBy).HasMaxLength(100);
        builder.Property(x => x.Details).HasMaxLength(2000);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponsePayload).IsRequired();
    }
}

public sealed class PdfTemplateConfiguration : IEntityTypeConfiguration<PdfTemplate>
{
    public void Configure(EntityTypeBuilder<PdfTemplate> builder)
    {
        builder.ToTable("pdf_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TradeName).HasMaxLength(250);
        builder.Property(x => x.PrimaryColor).HasMaxLength(7);
        builder.Property(x => x.FooterText).HasMaxLength(500);
        builder.Property(x => x.CommercialText).HasMaxLength(1000);
        builder.Property(x => x.LogoStorageKey).HasMaxLength(500);
    }
}
