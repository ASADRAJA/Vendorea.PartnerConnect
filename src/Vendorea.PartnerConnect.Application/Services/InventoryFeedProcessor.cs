using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Contracts.DTOs.CommercialData;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Validation.Core;
using Vendorea.PartnerConnect.Validation.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Processor for inventory feed operations including validation, transformation, and push to Merchant360.
/// </summary>
public class InventoryFeedProcessor : IInventoryFeedProcessor
{
    private readonly IDocumentValidator<InventoryUpdate> _validator;
    private readonly IMerchant360Client _merchant360Client;
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ILogger<InventoryFeedProcessor> _logger;

    public InventoryFeedProcessor(
        IDocumentValidator<InventoryUpdate> validator,
        IMerchant360Client merchant360Client,
        IPartnerDocumentRepository documentRepository,
        ILogger<InventoryFeedProcessor> logger)
    {
        _validator = validator;
        _merchant360Client = merchant360Client;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes a batch of inventory updates.
    /// </summary>
    public async Task<InventoryFeedProcessResult> ProcessAsync(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int dealerId,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {Count} inventory updates for dealer {DealerId}",
            inventoryUpdates.Count, dealerId);

        var validItems = new List<InventoryUpdate>();
        var invalidItems = new List<InventoryUpdateError>();
        var context = new ValidationContext { DealerId = dealerId };

        // Validate each item
        foreach (var inventoryUpdate in inventoryUpdates)
        {
            var validationResult = await _validator.ValidateAsync(inventoryUpdate, context, cancellationToken);

            if (validationResult.IsValid)
            {
                validItems.Add(inventoryUpdate with { Status = CanonicalStatus.Validated });
            }
            else
            {
                invalidItems.Add(new InventoryUpdateError(
                    inventoryUpdate.PartnerSku,
                    string.Join("; ", validationResult.Errors.Select(e => e.Message))));
            }
        }

        _logger.LogInformation(
            "Validation complete: {ValidCount} valid, {InvalidCount} invalid",
            validItems.Count, invalidItems.Count);

        // Push valid items to Merchant360
        var pushResult = await PushToMerchant360Async(validItems, dealerId, cancellationToken);

        // Update document status
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document != null)
        {
            document.ProcessedCount = validItems.Count;
            document.ErrorCount = invalidItems.Count + pushResult.FailedCount;
            document.Status = invalidItems.Count == 0 && pushResult.FailedCount == 0
                ? DocumentStatus.Completed
                : DocumentStatus.PartiallyCompleted;
            document.ProcessingCompletedAt = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(document, cancellationToken);
        }

        return new InventoryFeedProcessResult
        {
            TotalItems = inventoryUpdates.Count,
            ValidatedItems = validItems.Count,
            InvalidItems = invalidItems.Count,
            PushedItems = pushResult.SuccessCount,
            FailedPushItems = pushResult.FailedCount,
            ValidationErrors = invalidItems,
            Success = invalidItems.Count == 0 && pushResult.FailedCount == 0
        };
    }

    /// <summary>
    /// Validates a single inventory update.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        InventoryUpdate inventoryUpdate,
        CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext { DealerId = inventoryUpdate.DealerId };
        return await _validator.ValidateAsync(inventoryUpdate, context, cancellationToken);
    }

    /// <summary>
    /// Pushes validated inventory updates to Merchant360.
    /// </summary>
    public async Task<InventoryPushResult> PushToMerchant360Async(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        if (inventoryUpdates.Count == 0)
        {
            return new InventoryPushResult(0, 0);
        }

        var items = inventoryUpdates.Select(i => new InventoryUpdateItem(
            Sku: i.PartnerSku,
            QuantityAvailable: i.QuantityAvailable,
            QuantityOnOrder: i.QuantityOnOrder,
            WarehouseCode: i.WarehouseCode
        )).ToList();

        try
        {
            var result = await _merchant360Client.UpdateInventoryAsync(dealerId, items, cancellationToken);

            _logger.LogInformation(
                "Pushed {SuccessCount} inventory items to Merchant360, {ErrorCount} failed",
                items.Count - result.ErrorCount, result.ErrorCount);

            return new InventoryPushResult(items.Count - result.ErrorCount, result.ErrorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push inventory to Merchant360 for dealer {DealerId}", dealerId);
            return new InventoryPushResult(0, items.Count);
        }
    }

    /// <summary>
    /// Transforms raw inventory data to canonical format.
    /// </summary>
    public InventoryUpdate TransformToCanonical(
        Dictionary<string, string> rawData,
        int dealerId,
        string tradingPartnerCode,
        string sourceDocumentId)
    {
        return new InventoryUpdate
        {
            DealerId = dealerId,
            TradingPartnerCode = tradingPartnerCode,
            PartnerSku = rawData.GetValueOrDefault("sku") ?? string.Empty,
            Upc = rawData.GetValueOrDefault("upc"),
            ManufacturerPartNumber = rawData.GetValueOrDefault("mpn"),
            QuantityAvailable = int.TryParse(rawData.GetValueOrDefault("qty"), out var qty) ? qty : 0,
            QuantityOnOrder = int.TryParse(rawData.GetValueOrDefault("qtyOnOrder"), out var qtyOo) ? qtyOo : null,
            WarehouseCode = rawData.GetValueOrDefault("warehouse"),
            AvailabilityStatus = AvailabilityStatus.InStock,
            ReceivedAt = DateTime.UtcNow,
            SourceDocumentId = sourceDocumentId,
            Status = CanonicalStatus.Pending
        };
    }

    /// <summary>
    /// Aggregates inventory from multiple warehouses.
    /// </summary>
    public IReadOnlyList<InventoryUpdate> AggregateByWarehouse(
        IReadOnlyList<InventoryUpdate> inventoryUpdates)
    {
        return inventoryUpdates
            .GroupBy(i => new { i.DealerId, i.PartnerSku })
            .Select(g => g.First() with
            {
                QuantityAvailable = g.Sum(i => i.QuantityAvailable),
                QuantityOnOrder = g.Sum(i => i.QuantityOnOrder ?? 0),
                WarehouseCode = "AGGREGATE"
            })
            .ToList();
    }
}

/// <summary>
/// Interface for inventory feed processing.
/// </summary>
public interface IInventoryFeedProcessor
{
    Task<InventoryFeedProcessResult> ProcessAsync(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int dealerId,
        int documentId,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateAsync(
        InventoryUpdate inventoryUpdate,
        CancellationToken cancellationToken = default);

    Task<InventoryPushResult> PushToMerchant360Async(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int dealerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of inventory feed processing.
/// </summary>
public class InventoryFeedProcessResult
{
    public int TotalItems { get; init; }
    public int ValidatedItems { get; init; }
    public int InvalidItems { get; init; }
    public int PushedItems { get; init; }
    public int FailedPushItems { get; init; }
    public IReadOnlyList<InventoryUpdateError> ValidationErrors { get; init; } = Array.Empty<InventoryUpdateError>();
    public bool Success { get; init; }
}

/// <summary>
/// Inventory update validation error.
/// </summary>
public record InventoryUpdateError(string Sku, string ErrorMessage);

/// <summary>
/// Result of pushing inventory to Merchant360.
/// </summary>
public record InventoryPushResult(int SuccessCount, int FailedCount);
