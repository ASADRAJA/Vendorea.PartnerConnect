namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Unit of Work pattern interface for coordinating database transactions.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the trading partner repository.
    /// </summary>
    ITradingPartnerRepository TradingPartners { get; }

    /// <summary>
    /// Gets the dealer-partner connection repository.
    /// </summary>
    IDealerPartnerConnectionRepository DealerConnections { get; }

    /// <summary>
    /// Gets the partner document repository.
    /// </summary>
    IPartnerDocumentRepository PartnerDocuments { get; }

    /// <summary>
    /// Gets the document fingerprint repository.
    /// </summary>
    IDocumentFingerprintRepository DocumentFingerprints { get; }

    /// <summary>
    /// Gets the price feed batch repository.
    /// </summary>
    IPriceFeedBatchRepository PriceFeedBatches { get; }

    /// <summary>
    /// Gets the inventory feed batch repository.
    /// </summary>
    IInventoryFeedBatchRepository InventoryFeedBatches { get; }

    /// <summary>
    /// Gets the content sync job repository.
    /// </summary>
    IContentSyncJobRepository ContentSyncJobs { get; }

    /// <summary>
    /// Gets the document state history repository.
    /// </summary>
    IDocumentStateHistoryRepository DocumentStateHistory { get; }

    /// <summary>
    /// Gets the quarantined document repository.
    /// </summary>
    IQuarantinedDocumentRepository QuarantinedDocuments { get; }

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes work within a transaction, automatically handling commit/rollback.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes work within a transaction and returns a result, automatically handling commit/rollback.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}
