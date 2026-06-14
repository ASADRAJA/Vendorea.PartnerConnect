using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Persistence.Repositories;

namespace Vendorea.PartnerConnect.Persistence.UnitOfWork;

/// <summary>
/// Unit of Work implementation for coordinating database transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PartnerConnectDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    private ITradingPartnerRepository? _tradingPartners;
    private IPartnerDocumentRepository? _partnerDocuments;
    private IDocumentFingerprintRepository? _documentFingerprints;
    private IPriceFeedBatchRepository? _priceFeedBatches;
    private IInventoryFeedBatchRepository? _inventoryFeedBatches;
    private IContentSyncJobRepository? _contentSyncJobs;
    private IDocumentStateHistoryRepository? _documentStateHistory;
    private IQuarantinedDocumentRepository? _quarantinedDocuments;

    public UnitOfWork(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public ITradingPartnerRepository TradingPartners =>
        _tradingPartners ??= new TradingPartnerRepository(_context);

    public IPartnerDocumentRepository PartnerDocuments =>
        _partnerDocuments ??= new PartnerDocumentRepository(_context);

    public IDocumentFingerprintRepository DocumentFingerprints =>
        _documentFingerprints ??= new DocumentFingerprintRepository(_context);

    public IPriceFeedBatchRepository PriceFeedBatches =>
        _priceFeedBatches ??= new PriceFeedBatchRepository(_context);

    public IInventoryFeedBatchRepository InventoryFeedBatches =>
        _inventoryFeedBatches ??= new InventoryFeedBatchRepository(_context);

    public IContentSyncJobRepository ContentSyncJobs =>
        _contentSyncJobs ??= new ContentSyncJobRepository(_context);

    public IDocumentStateHistoryRepository DocumentStateHistory =>
        _documentStateHistory ??= new DocumentStateHistoryRepository(_context);

    public IQuarantinedDocumentRepository QuarantinedDocuments =>
        _quarantinedDocuments ??= new QuarantinedDocumentRepository(_context);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await BeginTransactionAsync(ct);

            try
            {
                await work(ct);
                await CommitAsync(ct);
            }
            catch
            {
                await RollbackAsync(ct);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            await BeginTransactionAsync(ct);

            try
            {
                var result = await work(ct);
                await CommitAsync(ct);
                return result;
            }
            catch
            {
                await RollbackAsync(ct);
                throw;
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
            }

            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        await _context.DisposeAsync();
    }
}
