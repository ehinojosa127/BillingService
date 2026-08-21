using Billing.Application.Abstractions;
using Billing.Application.Exceptions;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence.Repositories;

public sealed class IssuerRepository(BillingDbContext dbContext) : IIssuerRepository
{
    public Task<Issuer?> GetAsync(CancellationToken cancellationToken) =>
        dbContext.Issuers.OrderBy(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Issuer issuer, CancellationToken cancellationToken) =>
        await dbContext.Issuers.AddAsync(issuer, cancellationToken);

    public Task UpdateAsync(Issuer issuer, CancellationToken cancellationToken)
    {
        dbContext.Issuers.Update(issuer);
        return Task.CompletedTask;
    }
}

public sealed class DocumentSeriesRepository(BillingDbContext dbContext, IClock clock) : IDocumentSeriesRepository
{
    public Task<DocumentSeries?> GetAsync(DocumentType documentType, string series, CancellationToken cancellationToken) =>
        dbContext.DocumentSeries.FirstOrDefaultAsync(
            x => x.DocumentTypeCode == documentType.Code && x.Series == series.ToUpper(),
            cancellationToken);

    public async Task<IReadOnlyList<DocumentSeries>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.DocumentSeries.AsNoTracking().OrderBy(x => x.DocumentTypeCode).ThenBy(x => x.Series).ToListAsync(cancellationToken);

    public async Task AddAsync(DocumentSeries series, CancellationToken cancellationToken) =>
        await dbContext.DocumentSeries.AddAsync(series, cancellationToken);

    public Task<int> AllocateNextNumberAsync(DocumentType documentType, string series, CancellationToken cancellationToken) =>
        AllocateNextNumberAsync(documentType.Code, series, cancellationToken);

    public async Task<int> AllocateNextNumberAsync(string documentTypeCode, string series, CancellationToken cancellationToken)
    {
        var normalized = series.Trim().ToUpperInvariant();
        var id = Guid.CreateVersion7();
        var now = clock.UtcNow;

        try
        {
            var rows = await dbContext.Database
                .SqlQuery<int>($"""
                    INSERT INTO document_series (id, document_type_code, series, last_number, is_active, created_at)
                    VALUES ({id}, {documentTypeCode}, {normalized}, 1, TRUE, {now})
                    ON CONFLICT (document_type_code, series)
                    DO UPDATE SET last_number = document_series.last_number + 1
                    RETURNING last_number
                    """)
                .ToListAsync(cancellationToken);

            return rows[0];
        }
        catch (Exception ex)
        {
            throw new PersistenceException("Could not allocate the next document number.", ex);
        }
    }
}

public sealed class DocumentRepository(BillingDbContext dbContext) : IDocumentRepository
{
    public Task<ElectronicDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ElectronicDocument?> GetByNumberAsync(DocumentType documentType, string series, int number, CancellationToken cancellationToken) =>
        Query().FirstOrDefaultAsync(
            x => x.DocumentTypeCode == documentType.Code && x.Series == series && x.Number == number,
            cancellationToken);

    public async Task AddAsync(ElectronicDocument document, CancellationToken cancellationToken) =>
        await dbContext.Documents.AddAsync(document, cancellationToken);

    public Task UpdateAsync(ElectronicDocument document, CancellationToken cancellationToken)
    {
        var entry = dbContext.Entry(document);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Documents.Update(document);
            return Task.CompletedTask;
        }

        foreach (var file in document.Files)
        {
            if (dbContext.Entry(file).State == EntityState.Detached)
            {
                dbContext.GeneratedFiles.Add(file);
            }
        }

        var currentFileIds = document.Files.Select(file => file.Id).ToHashSet();
        var staleFiles = dbContext.GeneratedFiles
            .Where(file => file.DocumentId == document.Id && !currentFileIds.Contains(file.Id))
            .ToList();
        if (staleFiles.Count > 0)
        {
            dbContext.GeneratedFiles.RemoveRange(staleFiles);
        }

        foreach (var submission in document.Submissions)
        {
            if (dbContext.Entry(submission).State == EntityState.Detached)
            {
                dbContext.DocumentSubmissions.Add(submission);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<ElectronicDocument> Items, int Total)> SearchAsync(
        DocumentSearchFilter filter,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Documents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.DocumentTypeCode))
        {
            query = query.Where(x => x.DocumentTypeCode == filter.DocumentTypeCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.Series))
        {
            query = query.Where(x => x.Series == filter.Series.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(filter.ExternalReference))
        {
            query = query.Where(x => x.ExternalReference == filter.ExternalReference);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExternalId))
        {
            query = query.Where(x => x.ExternalId == filter.ExternalId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExternalSystem))
        {
            query = query.Where(x => x.ExternalSystem == filter.ExternalSystem);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && TryParseDocumentStatus(filter.Status, out var status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.SunatStatus) && TryParseSunatStatus(filter.SunatStatus, out var sunatStatus))
        {
            query = query.Where(x => x.SunatStatus == sunatStatus);
        }

        if (filter.DateFrom is not null)
        {
            query = query.Where(x => x.IssueDate >= filter.DateFrom);
        }

        if (filter.DateTo is not null)
        {
            query = query.Where(x => x.IssueDate <= filter.DateTo);
        }

        if (filter.MinAmount is not null)
        {
            query = query.Where(x => x.PayableAmount >= filter.MinAmount);
        }

        if (filter.MaxAmount is not null)
        {
            query = query.Where(x => x.PayableAmount <= filter.MaxAmount);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                (x.Series + "-" + x.Number.ToString()).Contains(term)
                || (x.RecipientIdentityNumber != null && x.RecipientIdentityNumber.Contains(term))
                || x.RecipientName.Contains(term)
                || (x.ExternalReference != null && x.ExternalReference.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip(filter.Skip).Take(filter.Take).ToListAsync(cancellationToken);
        return (items, total);
    }

    private static bool TryParseDocumentStatus(string value, out Domain.Enums.DocumentStatus status) =>
        Enum.TryParse(value, true, out status);

    private static bool TryParseSunatStatus(string value, out Domain.Enums.SunatStatus sunatStatus)
    {
        var normalized = value.Replace("-", string.Empty).Replace("_", string.Empty);
        return Enum.TryParse(normalized, true, out sunatStatus);
    }

    private IQueryable<ElectronicDocument> Query() =>
        dbContext.Documents
            .Include(x => x.Items)
            .Include(x => x.References)
            .Include(x => x.Submissions)
            .Include(x => x.Files);
}

public sealed class AuditLogRepository(BillingDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AddAsync(log, cancellationToken);
}

public sealed class IdempotencyStore(BillingDbContext dbContext) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken) =>
        dbContext.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken) =>
        await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);
}

public sealed class UnitOfWork(BillingDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}

public sealed class PdfTemplateRepository(BillingDbContext dbContext) : IPdfTemplateRepository
{
    public async Task<IReadOnlyList<PdfTemplate>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.PdfTemplates.OrderBy(x => x.Code).ToListAsync(cancellationToken);

    public Task<PdfTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.PdfTemplates.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public Task<PdfTemplate?> GetDefaultAsync(CancellationToken cancellationToken) =>
        dbContext.PdfTemplates.FirstOrDefaultAsync(x => x.IsDefault, cancellationToken);

    public async Task AddAsync(PdfTemplate template, CancellationToken cancellationToken) =>
        await dbContext.PdfTemplates.AddAsync(template, cancellationToken);

    public Task UpdateAsync(PdfTemplate template, CancellationToken cancellationToken)
    {
        dbContext.PdfTemplates.Update(template);
        return Task.CompletedTask;
    }
}
