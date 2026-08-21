using Billing.Domain.Catalogs;
using Billing.Domain.Entities;

namespace Billing.Application.Abstractions;

public interface IIssuerRepository
{
    Task<Issuer?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(Issuer issuer, CancellationToken cancellationToken);
    Task UpdateAsync(Issuer issuer, CancellationToken cancellationToken);
}

public interface IDocumentSeriesRepository
{
    Task<DocumentSeries?> GetAsync(DocumentType documentType, string series, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentSeries>> ListAsync(CancellationToken cancellationToken);
    Task AddAsync(DocumentSeries series, CancellationToken cancellationToken);
    Task<int> AllocateNextNumberAsync(DocumentType documentType, string series, CancellationToken cancellationToken);
    Task<int> AllocateNextNumberAsync(string documentTypeCode, string series, CancellationToken cancellationToken);
}

public interface IDocumentRepository
{
    Task<ElectronicDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ElectronicDocument?> GetByNumberAsync(DocumentType documentType, string series, int number, CancellationToken cancellationToken);
    Task AddAsync(ElectronicDocument document, CancellationToken cancellationToken);
    Task UpdateAsync(ElectronicDocument document, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ElectronicDocument> Items, int Total)> SearchAsync(
        DocumentSearchFilter filter,
        CancellationToken cancellationToken);
}

public sealed record DocumentSearchFilter(
    string? DocumentTypeCode,
    string? Series,
    string? Status,
    string? SunatStatus,
    string? ExternalReference,
    string? ExternalId,
    string? ExternalSystem,
    string? Search,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    decimal? MinAmount,
    decimal? MaxAmount,
    int Skip,
    int Take);

public interface IPdfTemplateRepository
{
    Task<IReadOnlyList<PdfTemplate>> ListAsync(CancellationToken cancellationToken);
    Task<PdfTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<PdfTemplate?> GetDefaultAsync(CancellationToken cancellationToken);
    Task AddAsync(PdfTemplate template, CancellationToken cancellationToken);
    Task UpdateAsync(PdfTemplate template, CancellationToken cancellationToken);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken);
}

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken);
    Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
