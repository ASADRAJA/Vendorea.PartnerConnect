using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository implementation for outbox messages.
/// </summary>
public class OutboxRepository : IOutboxRepository
{
    private readonly PartnerConnectDbContext _context;

    public OutboxRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.AddRange(messages);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetRetryDueAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Retry && m.NextRetryAt <= now)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.CorrelationId == correlationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.UpdateRange(messages);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CleanupDeliveredAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow - olderThan;
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Delivered && m.DeliveredAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);

        var stats = await _context.OutboxMessages
            .GroupBy(m => 1)
            .Select(g => new
            {
                PendingCount = g.Count(m => m.Status == OutboxMessageStatus.Pending),
                ProcessingCount = g.Count(m => m.Status == OutboxMessageStatus.Processing),
                RetryCount = g.Count(m => m.Status == OutboxMessageStatus.Retry),
                FailedCount = g.Count(m => m.Status == OutboxMessageStatus.Failed),
                DeliveredLast24Hours = g.Count(m => m.Status == OutboxMessageStatus.Delivered && m.DeliveredAt >= yesterday)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Calculate average delivery time for recent deliveries
        var avgDeliveryTime = await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Delivered && m.DeliveredAt >= yesterday)
            .Select(m => EF.Functions.DateDiffMillisecond(m.CreatedAt, m.DeliveredAt!.Value))
            .DefaultIfEmpty(0)
            .AverageAsync(cancellationToken);

        return new OutboxStatistics
        {
            PendingCount = stats?.PendingCount ?? 0,
            ProcessingCount = stats?.ProcessingCount ?? 0,
            RetryCount = stats?.RetryCount ?? 0,
            FailedCount = stats?.FailedCount ?? 0,
            DeliveredLast24Hours = stats?.DeliveredLast24Hours ?? 0,
            AverageDeliveryTimeMs = avgDeliveryTime
        };
    }
}
