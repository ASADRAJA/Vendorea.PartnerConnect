using System.ComponentModel;
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
/// <remarks>
/// Phase 2 - Inventory push to Merchant360 is not implemented in Phase 1.
/// This processor is retained for internal validation and future use.
/// </remarks>
[Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
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
    /// <remarks>Phase 2 - Merchant360 push is disabled.</remarks>
    [Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<InventoryFeedProcessResult> ProcessAsync(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int merchantId,
        int documentId,
        int tradingPartnerId,
        string tradingPartnerCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Inventory push is Phase 2. Processing {Count} inventory updates locally without Merchant360 push.",
            inventoryUpdates.Count);

        var validItems = new List<InventoryUpdate>();
        var invalidItems = new List<InventoryUpdateError>();
        var context = new ValidationContext { DealerId = merchantId };

        // Validate each item (validation still works, just no push)
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
            "Validation complete: {ValidCount} valid, {InvalidCount} invalid (no Merchant360 push - Phase 2)",
            validItems.Count, invalidItems.Count);

        // Update document status (no Merchant360 push in Phase 1)
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document != null)
        {
            document.ProcessedCount = validItems.Count;
            document.ErrorCount = invalidItems.Count;
            document.Status = invalidItems.Count == 0
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
            PushedItems = 0, // No push in Phase 1
            FailedPushItems = 0,
            ValidationErrors = invalidItems,
            Success = invalidItems.Count == 0
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
    /// <remarks>Phase 2 - Not implemented. Always returns failure result.</remarks>
    [Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<InventoryPushResult> PushToMerchant360Async(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int merchantId,
        int tradingPartnerId,
        string tradingPartnerCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Inventory push to Merchant360 is disabled in Phase 1. Skipping {Count} items.",
            inventoryUpdates.Count);

        // Return success with 0 pushed - inventory is simply not pushed in Phase 1
        return Task.FromResult(new InventoryPushResult(0, 0));
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
            PartnerSku = rawData.GetValueOrDefault("stockNumber") ?? rawData.GetValueOrDefault("sku") ?? string.Empty,
            Upc = rawData.GetValueOrDefault("upcCode") ?? rawData.GetValueOrDefault("upc"),
            ManufacturerPartNumber = rawData.GetValueOrDefault("manufacturerPartNumber") ?? rawData.GetValueOrDefault("mpn"),
            QuantityAvailable = int.TryParse(rawData.GetValueOrDefault("qty"), out var qty) ? qty : 0,
            QuantityOnOrder = int.TryParse(rawData.GetValueOrDefault("qtyOnOrder"), out var qtyOo) ? qtyOo : null,
            WarehouseCode = rawData.GetValueOrDefault("warehouseCode") ?? rawData.GetValueOrDefault("warehouse"),
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
/// <remarks>Phase 2 - Inventory push is not implemented in Phase 1.</remarks>
[Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
public interface IInventoryFeedProcessor
{
    [Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
    Task<InventoryFeedProcessResult> ProcessAsync(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int merchantId,
        int documentId,
        int tradingPartnerId,
        string tradingPartnerCode,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateAsync(
        InventoryUpdate inventoryUpdate,
        CancellationToken cancellationToken = default);

    [Obsolete("Inventory push is Phase 2. Do not use for Merchant360 integration in Phase 1.")]
    Task<InventoryPushResult> PushToMerchant360Async(
        IReadOnlyList<InventoryUpdate> inventoryUpdates,
        int merchantId,
        int tradingPartnerId,
        string tradingPartnerCode,
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
