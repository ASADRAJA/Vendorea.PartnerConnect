using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Assembles a tenant's activity feed from the real tenant-scoped tables. There is no single
/// "activity" table, so each source (price-feed uploads, order status changes, connection state,
/// quarantined documents) is queried independently.
///
/// Paging model: every source query filters by tenant + date + level in SQL and is ordered newest
/// first, and each fetches at most <c>skip + take</c> rows (also in SQL). The <c>Total</c> is the
/// exact sum of per-source SQL counts. The final cross-source merge/sort of at most
/// <c>(skip + take) × sources</c> rows happens in memory — the only step a heterogeneous union
/// can't push to a single SQL statement.
/// </summary>
public class ActivityRepository : IActivityRepository
{
    private const string TypePriceFeed = "PriceFeed";
    private const string TypeOrder = "Order";
    private const string TypeConnection = "Connection";
    private const string TypeException = "Exception";

    private const string LevelInfo = "Info";
    private const string LevelWarning = "Warning";
    private const string LevelError = "Error";

    private readonly PartnerConnectDbContext _context;

    public ActivityRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ActivityEvent> Items, int Total)> GetTenantActivityPageAsync(
        int tenantId,
        string? type,
        string? level,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var fetch = skip + take;
        var events = new List<ActivityEvent>(fetch * 4);
        var total = 0;

        if (Wants(type, TypePriceFeed))
        {
            var (rows, count) = await PriceFeedAsync(tenantId, level, from, to, fetch, cancellationToken);
            events.AddRange(rows);
            total += count;
        }

        if (Wants(type, TypeOrder))
        {
            var (rows, count) = await OrderAsync(tenantId, level, from, to, fetch, cancellationToken);
            events.AddRange(rows);
            total += count;
        }

        if (Wants(type, TypeConnection))
        {
            var (rows, count) = await ConnectionAsync(tenantId, level, from, to, fetch, cancellationToken);
            events.AddRange(rows);
            total += count;
        }

        if (Wants(type, TypeException))
        {
            var (rows, count) = await ExceptionAsync(tenantId, level, from, to, fetch, cancellationToken);
            events.AddRange(rows);
            total += count;
        }

        var page = events
            .OrderByDescending(e => e.At)
            .Skip(skip)
            .Take(take)
            .ToList();

        return (page, total);
    }

    private static bool Wants(string? type, string source)
        => string.IsNullOrWhiteSpace(type) || string.Equals(type, source, StringComparison.OrdinalIgnoreCase);

    // ----- Price-feed uploads (DealerId == tenant) --------------------------------------------
    private async Task<(List<ActivityEvent> Rows, int Count)> PriceFeedAsync(
        int tenantId, string? level, DateTime? from, DateTime? to, int fetch, CancellationToken ct)
    {
        var q = _context.PriceFeedUploads.Where(u => u.DealerId == tenantId);
        if (from.HasValue) q = q.Where(u => u.UploadedAt >= from.Value);
        if (to.HasValue) q = q.Where(u => u.UploadedAt <= to.Value);

        // Error when the upload failed to parse or push; otherwise Info (never Warning).
        if (!string.IsNullOrWhiteSpace(level))
        {
            if (string.Equals(level, LevelError, StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => u.Status == PriceFeedUploadStatus.Failed || u.Status == PriceFeedUploadStatus.PushFailed);
            else if (string.Equals(level, LevelInfo, StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => u.Status != PriceFeedUploadStatus.Failed && u.Status != PriceFeedUploadStatus.PushFailed);
            else
                return (new List<ActivityEvent>(), 0); // Warning-only filter: price feeds never warn
        }

        var count = await q.CountAsync(ct);
        var raw = await q
            .OrderByDescending(u => u.UploadedAt)
            .Take(fetch)
            .Select(u => new
            {
                u.UploadedAt,
                u.Status,
                u.FileName,
                u.RecordCount,
                u.ErrorCount,
                u.ErrorMessage,
                u.CorrelationId
            })
            .ToListAsync(ct);

        var rows = raw.Select(u =>
        {
            var isError = u.Status is PriceFeedUploadStatus.Failed or PriceFeedUploadStatus.PushFailed;
            return new ActivityEvent
            {
                At = u.UploadedAt,
                Type = TypePriceFeed,
                Level = isError ? LevelError : LevelInfo,
                Title = $"Price feed {StatusText(u.Status)}",
                Detail = isError
                    ? (u.ErrorMessage ?? $"{u.ErrorCount} record(s) failed") + (string.IsNullOrWhiteSpace(u.FileName) ? "" : $" — {u.FileName}")
                    : $"{u.RecordCount:N0} record(s)" + (string.IsNullOrWhiteSpace(u.FileName) ? "" : $" — {u.FileName}"),
                CorrelationId = u.CorrelationId,
                Link = "/catalog/prices"
            };
        }).ToList();

        return (rows, count);
    }

    // ----- Order status changes (Order.TenantId == tenant) ------------------------------------
    private async Task<(List<ActivityEvent> Rows, int Count)> OrderAsync(
        int tenantId, string? level, DateTime? from, DateTime? to, int fetch, CancellationToken ct)
    {
        var q =
            from h in _context.OrderStatusHistory
            join o in _context.Orders on h.OrderId equals o.Id
            where o.TenantId == tenantId
            select new { h.ChangedAt, h.ToStatus, h.Reason, h.Source, o.PoNumber, OrderId = o.Id, o.CorrelationId };

        if (from.HasValue) q = q.Where(x => x.ChangedAt >= from.Value);
        if (to.HasValue) q = q.Where(x => x.ChangedAt <= to.Value);

        // Failed → Error, Cancelled → Warning, otherwise Info.
        if (!string.IsNullOrWhiteSpace(level))
        {
            q = level switch
            {
                _ when string.Equals(level, LevelError, StringComparison.OrdinalIgnoreCase)
                    => q.Where(x => x.ToStatus == OrderStatus.Failed),
                _ when string.Equals(level, LevelWarning, StringComparison.OrdinalIgnoreCase)
                    => q.Where(x => x.ToStatus == OrderStatus.Cancelled),
                _ => q.Where(x => x.ToStatus != OrderStatus.Failed && x.ToStatus != OrderStatus.Cancelled)
            };
        }

        var count = await q.CountAsync(ct);
        var raw = await q
            .OrderByDescending(x => x.ChangedAt)
            .Take(fetch)
            .ToListAsync(ct);

        var rows = raw.Select(x => new ActivityEvent
        {
            At = x.ChangedAt,
            Type = TypeOrder,
            Level = x.ToStatus switch
            {
                OrderStatus.Failed => LevelError,
                OrderStatus.Cancelled => LevelWarning,
                _ => LevelInfo
            },
            Title = $"Order {x.PoNumber} {x.ToStatus}",
            Detail = string.IsNullOrWhiteSpace(x.Reason) ? x.Source : x.Reason,
            CorrelationId = x.CorrelationId.ToString(),
            Link = $"/orders/{x.OrderId}"
        }).ToList();

        return (rows, count);
    }

    // ----- Connection state (one event per tenant-partner account) ----------------------------
    private async Task<(List<ActivityEvent> Rows, int Count)> ConnectionAsync(
        int tenantId, string? level, DateTime? from, DateTime? to, int fetch, CancellationToken ct)
    {
        var q = _context.TenantPartnerAccounts
            .Where(c => c.TenantId == tenantId)
            .Select(c => new
            {
                At = c.DecidedAt ?? c.CreatedAt,
                c.ApprovalStatus,
                PartnerName = c.TradingPartner!.Name,
                c.DecisionReason
            });

        if (from.HasValue) q = q.Where(x => x.At >= from.Value);
        if (to.HasValue) q = q.Where(x => x.At <= to.Value);

        // Denied/Cancelled → Warning, otherwise Info.
        if (!string.IsNullOrWhiteSpace(level))
        {
            if (string.Equals(level, LevelWarning, StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.ApprovalStatus == ConnectionApprovalStatus.Denied || x.ApprovalStatus == ConnectionApprovalStatus.Cancelled);
            else if (string.Equals(level, LevelInfo, StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.ApprovalStatus != ConnectionApprovalStatus.Denied && x.ApprovalStatus != ConnectionApprovalStatus.Cancelled);
            else
                return (new List<ActivityEvent>(), 0); // Error-only filter: connections never error
        }

        var count = await q.CountAsync(ct);
        var raw = await q
            .OrderByDescending(x => x.At)
            .Take(fetch)
            .ToListAsync(ct);

        var rows = raw.Select(x => new ActivityEvent
        {
            At = x.At,
            Type = TypeConnection,
            Level = x.ApprovalStatus is ConnectionApprovalStatus.Denied or ConnectionApprovalStatus.Cancelled
                ? LevelWarning
                : LevelInfo,
            Title = $"Connection to {x.PartnerName} {x.ApprovalStatus}",
            Detail = x.DecisionReason,
            CorrelationId = null,
            Link = "/connections"
        }).ToList();

        return (rows, count);
    }

    // ----- Quarantined documents (Exception) --------------------------------------------------
    private async Task<(List<ActivityEvent> Rows, int Count)> ExceptionAsync(
        int tenantId, string? level, DateTime? from, DateTime? to, int fetch, CancellationToken ct)
    {
        var q = _context.QuarantinedDocuments
            .Where(d => d.TenantId == tenantId)
            .Select(d => new
            {
                d.QuarantinedAt,
                d.Reason,
                d.ErrorMessage,
                Resolved = d.Resolution != null,
                CorrelationId = d.PartnerDocument != null ? d.PartnerDocument.CorrelationId : null
            });

        if (from.HasValue) q = q.Where(x => x.QuarantinedAt >= from.Value);
        if (to.HasValue) q = q.Where(x => x.QuarantinedAt <= to.Value);

        // Unresolved → Error, resolved → Warning.
        if (!string.IsNullOrWhiteSpace(level))
        {
            if (string.Equals(level, LevelError, StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => !x.Resolved);
            else if (string.Equals(level, LevelWarning, StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.Resolved);
            else
                return (new List<ActivityEvent>(), 0); // Info-only filter: quarantines are never Info
        }

        var count = await q.CountAsync(ct);
        var raw = await q
            .OrderByDescending(x => x.QuarantinedAt)
            .Take(fetch)
            .ToListAsync(ct);

        var rows = raw.Select(x => new ActivityEvent
        {
            At = x.QuarantinedAt,
            Type = TypeException,
            Level = x.Resolved ? LevelWarning : LevelError,
            Title = $"Document quarantined: {ReasonText(x.Reason)}",
            Detail = x.ErrorMessage,
            CorrelationId = x.CorrelationId,
            Link = null
        }).ToList();

        return (rows, count);
    }

    private static string StatusText(PriceFeedUploadStatus status) => status switch
    {
        PriceFeedUploadStatus.Pending => "received",
        PriceFeedUploadStatus.Processing => "processing",
        PriceFeedUploadStatus.Completed => "processed",
        PriceFeedUploadStatus.Failed => "failed",
        PriceFeedUploadStatus.PushedToMerchant360 => "published",
        PriceFeedUploadStatus.PushFailed => "publish failed",
        PriceFeedUploadStatus.Cancelled => "cancelled",
        PriceFeedUploadStatus.PushQueued => "queued for publish",
        PriceFeedUploadStatus.Pushing => "publishing",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string ReasonText(QuarantineReason reason) => reason switch
    {
        QuarantineReason.ValidationFailed => "validation failed",
        QuarantineReason.MappingFailed => "could not be mapped",
        QuarantineReason.DeliveryFailed => "delivery failed",
        QuarantineReason.Rejected => "rejected by recipient",
        QuarantineReason.MaxRetriesExceeded => "retries exhausted",
        QuarantineReason.DuplicateDetected => "duplicate detected",
        QuarantineReason.InvalidFormat => "invalid format",
        QuarantineReason.ConfigurationMissing => "configuration missing",
        QuarantineReason.ManualQuarantine => "manually held",
        _ => reason.ToString()
    };
}
