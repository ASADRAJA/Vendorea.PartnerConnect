using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for processing full-refresh inventory snapshots.
/// Implements staging-to-production workflow with atomic swap.
/// </summary>
public class InventoryFullRefreshService : IInventoryFullRefreshService
{
    private readonly ISupplierInventorySnapshotRepository _snapshotRepository;
    private readonly ISupplierInventoryItemRepository _itemRepository;
    private readonly ITenantPartnerAccountRepository _tenantPartnerAccountRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<InventoryFullRefreshService> _logger;
    private const int MaxValidationErrorsToStore = 100;

    public InventoryFullRefreshService(
        ISupplierInventorySnapshotRepository snapshotRepository,
        ISupplierInventoryItemRepository itemRepository,
        ITenantPartnerAccountRepository tenantPartnerAccountRepository,
        ITenantRepository tenantRepository,
        IOutboxService outboxService,
        ILogger<InventoryFullRefreshService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _itemRepository = itemRepository;
        _tenantPartnerAccountRepository = tenantPartnerAccountRepository;
        _tenantRepository = tenantRepository;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<SupplierInventorySnapshot> CreateSnapshotAsync(
        int tradingPartnerId,
        string fileName,
        DateTime inventoryDate,
        int? partnerDocumentId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new SupplierInventorySnapshot
        {
            TradingPartnerId = tradingPartnerId,
            SnapshotId = fileName,
            InventoryDate = inventoryDate,
            PartnerDocumentId = partnerDocumentId,
            Status = InventorySnapshotStatus.Received,
            IsFullRefresh = true,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Get previous snapshot for linking
        var previousSnapshot = await GetCurrentSnapshotAsync(tradingPartnerId, cancellationToken);
        if (previousSnapshot != null)
        {
            snapshot.PreviousSnapshotId = previousSnapshot.Id;
        }

        await _snapshotRepository.AddAsync(snapshot, cancellationToken);

        _logger.LogInformation(
            "Created inventory snapshot {SnapshotId} for partner {PartnerId}, CorrelationId={CorrelationId}",
            snapshot.Id, tradingPartnerId, snapshot.CorrelationId);

        return snapshot;
    }

    public async Task<InventoryValidationResult> ValidateAndStageAsync(
        int snapshotId,
        IEnumerable<SupplierInventoryItem> items,
        CancellationToken cancellationToken = default)
    {
        var result = new InventoryValidationResult();
        var itemList = items.ToList();
        result.TotalItems = itemList.Count;

        var snapshot = await _snapshotRepository.GetByIdAsync(snapshotId, cancellationToken);
        if (snapshot == null)
        {
            result.Errors.Add(new InventoryItemError { Message = "Snapshot not found" });
            return result;
        }

        // Transition to Validating
        snapshot.Status = InventorySnapshotStatus.Validating;
        snapshot.ProcessingStartedAt = DateTime.UtcNow;
        snapshot.TotalItemCount = itemList.Count;
        await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);

        _logger.LogInformation(
            "Starting validation for snapshot {SnapshotId} with {ItemCount} items",
            snapshotId, itemList.Count);

        try
        {
            var errors = new List<InventoryItemError>();
            var validItems = new List<SupplierInventoryItem>();
            var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                item.SupplierInventorySnapshotId = snapshotId;

                var itemErrors = ValidateItem(item, i + 1, seenSkus);
                if (itemErrors.Count > 0)
                {
                    if (errors.Count < MaxValidationErrorsToStore)
                    {
                        errors.AddRange(itemErrors);
                    }
                }
                else
                {
                    validItems.Add(item);
                    seenSkus.Add(item.SupplierSku);
                }
            }

            result.ValidItems = validItems.Count;
            result.InvalidItems = itemList.Count - validItems.Count;
            result.Errors = errors.Take(MaxValidationErrorsToStore).ToList();

            // Determine if we should proceed to staging
            // Allow staging if at least 90% of items are valid
            var validPercentage = (double)validItems.Count / itemList.Count * 100;
            result.IsValid = validPercentage >= 90;

            if (result.IsValid)
            {
                // Save valid items (staging)
                await _itemRepository.AddRangeAsync(validItems, cancellationToken);

                snapshot.Status = InventorySnapshotStatus.Staging;
                snapshot.ProcessedItemCount = validItems.Count;
                snapshot.ErrorCount = result.InvalidItems;
                result.ResultStatus = InventorySnapshotStatus.Staging;

                _logger.LogInformation(
                    "Staged {ValidCount}/{TotalCount} items for snapshot {SnapshotId}",
                    validItems.Count, itemList.Count, snapshotId);
            }
            else
            {
                snapshot.Status = InventorySnapshotStatus.ValidationFailed;
                snapshot.ErrorMessage = $"Validation failed: {result.InvalidItems} invalid items ({100 - validPercentage:F1}% error rate)";
                result.ResultStatus = InventorySnapshotStatus.ValidationFailed;

                _logger.LogWarning(
                    "Validation failed for snapshot {SnapshotId}: {ErrorCount} errors",
                    snapshotId, result.InvalidItems);
            }

            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation of snapshot {SnapshotId}", snapshotId);
            await MarkFailedAsync(snapshotId, $"Validation error: {ex.Message}", cancellationToken);
            result.IsValid = false;
            result.ResultStatus = InventorySnapshotStatus.Failed;
            result.Errors.Add(new InventoryItemError { Message = ex.Message, Category = "System" });
        }

        return result;
    }

    public async Task<InventoryApplyResult> ApplySnapshotAsync(
        int snapshotId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new InventoryApplyResult { SnapshotId = snapshotId };

        var snapshot = await _snapshotRepository.GetByIdWithItemsAsync(snapshotId, cancellationToken);
        if (snapshot == null)
        {
            result.ErrorMessage = "Snapshot not found";
            return result;
        }

        if (snapshot.Status != InventorySnapshotStatus.Staging)
        {
            result.ErrorMessage = $"Snapshot is in {snapshot.Status} status, expected Staging";
            return result;
        }

        _logger.LogInformation(
            "Applying snapshot {SnapshotId} with {ItemCount} items",
            snapshotId, snapshot.Items.Count);

        try
        {
            // Transition to Applying
            snapshot.Status = InventorySnapshotStatus.Applying;
            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);

            // Get previous snapshot items for comparison
            SupplierInventorySnapshot? previousSnapshot = null;
            var previousItemsMap = new Dictionary<string, SupplierInventoryItem>(StringComparer.OrdinalIgnoreCase);

            if (snapshot.PreviousSnapshotId.HasValue)
            {
                previousSnapshot = await _snapshotRepository.GetByIdWithItemsAsync(
                    snapshot.PreviousSnapshotId.Value, cancellationToken);

                if (previousSnapshot != null)
                {
                    foreach (var item in previousSnapshot.Items)
                    {
                        previousItemsMap[item.SupplierSku] = item;
                    }
                }
            }

            // Calculate change counts (used by the lightweight snapshot-applied notification).
            var currentSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in snapshot.Items)
            {
                currentSkus.Add(item.SupplierSku);

                if (previousItemsMap.TryGetValue(item.SupplierSku, out var prevItem))
                {
                    if (HasChanged(item, prevItem))
                    {
                        result.UpdatedItems++;
                    }
                    else
                    {
                        result.UnchangedItems++;
                    }
                }
                else
                {
                    result.NewItems++;
                }
            }

            // Items in previous but not in current are "removed".
            foreach (var prevSku in previousItemsMap.Keys)
            {
                if (!currentSkus.Contains(prevSku))
                {
                    result.RemovedItems++;
                }
            }

            // Mark snapshot as Applied
            snapshot.Status = InventorySnapshotStatus.Applied;
            snapshot.ProcessingCompletedAt = DateTime.UtcNow;
            snapshot.NewItemCount = result.NewItems;
            snapshot.UpdatedItemCount = result.UpdatedItems;
            snapshot.RemovedItemCount = result.RemovedItems;
            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);

            // Supersede previous snapshots
            if (previousSnapshot != null)
            {
                result.SupersededSnapshotId = previousSnapshot.Id;
                await SupersedePreviousSnapshotsAsync(
                    snapshot.TradingPartnerId, snapshotId, cancellationToken);
            }

            result.Success = true;
            stopwatch.Stop();
            result.ApplyTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Applied snapshot {SnapshotId}: New={New}, Updated={Updated}, Removed={Removed}, Unchanged={Unchanged}, Time={TimeMs}ms",
                snapshotId, result.NewItems, result.UpdatedItems, result.RemovedItems, result.UnchangedItems, result.ApplyTimeMs);

            // Notify every subscribed merchant that a snapshot was applied (lightweight counts).
            // Non-fatal: a callback-enqueue failure must not undo a successfully applied snapshot.
            try
            {
                await EnqueueInventorySnapshotNotificationsAsync(snapshot, result, cancellationToken);
            }
            catch (Exception cbEx)
            {
                _logger.LogError(cbEx,
                    "Failed to enqueue inventory snapshot notifications for snapshot {SnapshotId} (non-fatal)", snapshotId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying snapshot {SnapshotId}", snapshotId);
            await MarkFailedAsync(snapshotId, $"Apply error: {ex.Message}", cancellationToken);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Enqueues a lightweight "snapshot applied" notification (summary counts only) to Merchant360
    /// via the outbox, fanned out to every active tenant account on the snapshot's trading partner.
    /// </summary>
    private async Task EnqueueInventorySnapshotNotificationsAsync(
        SupplierInventorySnapshot snapshot,
        InventoryApplyResult result,
        CancellationToken cancellationToken)
    {
        var accounts = await _tenantPartnerAccountRepository.GetByTradingPartnerIdAsync(
            snapshot.TradingPartnerId, cancellationToken);
        var pcTenantIds = accounts
            .Where(a => a.IsActive)
            .Select(a => a.TenantId)
            .Distinct()
            .ToList();

        if (pcTenantIds.Count == 0)
        {
            _logger.LogInformation(
                "Snapshot {SnapshotId} applied but no active merchants subscribe to partner {PartnerId}",
                snapshot.Id, snapshot.TradingPartnerId);
            return;
        }

        var totalItems = result.NewItems + result.UpdatedItems + result.UnchangedItems;
        var enqueued = 0;
        foreach (var pcTenantId in pcTenantIds)
        {
            // The M360 callback route is scoped by the M360 merchant id, which PC stores as
            // Tenant.ExternalId (synced from M360) — not PC's internal tenant id.
            var merchantId = await ResolveMerchantIdAsync(pcTenantId, cancellationToken);
            if (merchantId == null)
            {
                _logger.LogWarning(
                    "Tenant {TenantId} has no numeric ExternalId; skipping inventory snapshot callback", pcTenantId);
                continue;
            }

            var request = new SupplierInventorySnapshotNotificationRequest
            {
                EventId = Guid.NewGuid().ToString(),
                TradingPartnerId = snapshot.TradingPartnerId,
                SnapshotId = snapshot.Id,
                ItemCount = totalItems,
                GeneratedAt = snapshot.InventoryDate
            };

            await _outboxService.EnqueueAsync(
                Merchant360OutboxMessageTypes.InventorySnapshot,
                new Merchant360InventorySnapshotOutboxPayload { MerchantId = merchantId.Value, Request = request },
                correlationId: snapshot.CorrelationId,
                cancellationToken: cancellationToken);
            enqueued++;
        }

        _logger.LogInformation(
            "Enqueued {Enqueued} inventory snapshot notification(s) for snapshot {SnapshotId} (new={New}, updated={Updated}, removed={Removed})",
            enqueued, snapshot.Id, result.NewItems, result.UpdatedItems, result.RemovedItems);
    }

    /// <summary>
    /// Resolves the M360 merchant id (the callback route scope) for a PC tenant id via
    /// Tenant.ExternalId. Returns null when the tenant has no numeric external id.
    /// </summary>
    private async Task<int?> ResolveMerchantIdAsync(int pcTenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(pcTenantId, cancellationToken);
        if (tenant?.ExternalId != null && int.TryParse(tenant.ExternalId, out var merchantId))
        {
            return merchantId;
        }
        return null;
    }

    public async Task<SupplierInventorySnapshot?> GetCurrentSnapshotAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _snapshotRepository.GetLatestAppliedAsync(tradingPartnerId, cancellationToken);
    }

    public async Task<SupplierInventorySnapshot?> GetSnapshotAsync(
        int snapshotId,
        bool includeItems = false,
        CancellationToken cancellationToken = default)
    {
        return includeItems
            ? await _snapshotRepository.GetByIdWithItemsAsync(snapshotId, cancellationToken)
            : await _snapshotRepository.GetByIdAsync(snapshotId, cancellationToken);
    }

    public async Task MarkFailedAsync(
        int snapshotId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.GetByIdAsync(snapshotId, cancellationToken);
        if (snapshot != null)
        {
            snapshot.Status = InventorySnapshotStatus.Failed;
            snapshot.ErrorMessage = errorMessage;
            snapshot.ProcessingCompletedAt = DateTime.UtcNow;
            await _snapshotRepository.UpdateAsync(snapshot, cancellationToken);

            _logger.LogWarning("Marked snapshot {SnapshotId} as failed: {Error}", snapshotId, errorMessage);
        }
    }

    public async Task SupersedePreviousSnapshotsAsync(
        int tradingPartnerId,
        int excludeSnapshotId,
        CancellationToken cancellationToken = default)
    {
        await _snapshotRepository.SupersedeAllExceptAsync(tradingPartnerId, excludeSnapshotId, cancellationToken);

        _logger.LogInformation(
            "Superseded previous snapshots for partner {PartnerId}, keeping {SnapshotId}",
            tradingPartnerId, excludeSnapshotId);
    }

    private static List<InventoryItemError> ValidateItem(
        SupplierInventoryItem item,
        int lineNumber,
        HashSet<string> seenSkus)
    {
        var errors = new List<InventoryItemError>();

        // Required SKU
        if (string.IsNullOrWhiteSpace(item.SupplierSku))
        {
            errors.Add(new InventoryItemError
            {
                Sku = "(empty)",
                Message = "Partner SKU is required",
                Category = "Required",
                LineNumber = lineNumber
            });
            return errors; // Can't continue without SKU
        }

        // Duplicate SKU
        if (seenSkus.Contains(item.SupplierSku))
        {
            errors.Add(new InventoryItemError
            {
                Sku = item.SupplierSku,
                Message = "Duplicate SKU in snapshot",
                Category = "Duplicate",
                LineNumber = lineNumber
            });
        }

        // Quantity validation
        if (item.QuantityAvailable < 0)
        {
            errors.Add(new InventoryItemError
            {
                Sku = item.SupplierSku,
                Message = "Quantity available cannot be negative",
                Category = "Range",
                LineNumber = lineNumber
            });
        }

        // Validate UPC if provided
        if (!string.IsNullOrWhiteSpace(item.Upc) && !IsValidUpc(item.Upc))
        {
            errors.Add(new InventoryItemError
            {
                Sku = item.SupplierSku,
                Message = $"Invalid UPC format: {item.Upc}",
                Category = "Format",
                LineNumber = lineNumber
            });
        }

        return errors;
    }

    private static bool IsValidUpc(string upc)
    {
        // Basic UPC validation: should be 12 or 13 digits
        if (string.IsNullOrWhiteSpace(upc))
            return true; // Optional field

        var digitsOnly = new string(upc.Where(char.IsDigit).ToArray());
        return digitsOnly.Length == 12 || digitsOnly.Length == 13;
    }

    private static bool HasChanged(SupplierInventoryItem current, SupplierInventoryItem previous)
    {
        return current.QuantityAvailable != previous.QuantityAvailable ||
               current.QuantityOnOrder != previous.QuantityOnOrder ||
               current.Status != previous.Status ||
               current.UnitCost != previous.UnitCost;
    }
}
