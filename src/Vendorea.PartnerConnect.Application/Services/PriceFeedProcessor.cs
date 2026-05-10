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
/// Processor for price feed operations including validation, transformation, and push to Merchant360.
/// </summary>
public class PriceFeedProcessor : IPriceFeedProcessor
{
    private readonly IDocumentValidator<PriceUpdate> _validator;
    private readonly IMerchant360Client _merchant360Client;
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ILogger<PriceFeedProcessor> _logger;

    public PriceFeedProcessor(
        IDocumentValidator<PriceUpdate> validator,
        IMerchant360Client merchant360Client,
        IPartnerDocumentRepository documentRepository,
        ILogger<PriceFeedProcessor> logger)
    {
        _validator = validator;
        _merchant360Client = merchant360Client;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes a batch of price updates.
    /// </summary>
    public async Task<PriceFeedProcessResult> ProcessAsync(
        IReadOnlyList<PriceUpdate> priceUpdates,
        int dealerId,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {Count} price updates for dealer {DealerId}",
            priceUpdates.Count, dealerId);

        var validItems = new List<PriceUpdate>();
        var invalidItems = new List<PriceUpdateError>();
        var context = new ValidationContext { DealerId = dealerId };

        // Validate each item
        foreach (var priceUpdate in priceUpdates)
        {
            var validationResult = await _validator.ValidateAsync(priceUpdate, context, cancellationToken);

            if (validationResult.IsValid)
            {
                validItems.Add(priceUpdate with { Status = CanonicalStatus.Validated });
            }
            else
            {
                invalidItems.Add(new PriceUpdateError(
                    priceUpdate.PartnerSku,
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

        return new PriceFeedProcessResult
        {
            TotalItems = priceUpdates.Count,
            ValidatedItems = validItems.Count,
            InvalidItems = invalidItems.Count,
            PushedItems = pushResult.SuccessCount,
            FailedPushItems = pushResult.FailedCount,
            ValidationErrors = invalidItems,
            Success = invalidItems.Count == 0 && pushResult.FailedCount == 0
        };
    }

    /// <summary>
    /// Validates a single price update.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        PriceUpdate priceUpdate,
        CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext { DealerId = priceUpdate.DealerId };
        return await _validator.ValidateAsync(priceUpdate, context, cancellationToken);
    }

    /// <summary>
    /// Pushes validated price updates to Merchant360.
    /// </summary>
    public async Task<PushResult> PushToMerchant360Async(
        IReadOnlyList<PriceUpdate> priceUpdates,
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        if (priceUpdates.Count == 0)
        {
            return new PushResult(0, 0);
        }

        var items = priceUpdates.Select(p => new PriceUpdateItem(
            Sku: p.PartnerSku,
            Cost: p.Cost,
            ListPrice: p.ListPrice,
            CurrencyCode: p.Currency.ToString()
        )).ToList();

        try
        {
            var result = await _merchant360Client.UpdatePricesAsync(dealerId, items, cancellationToken);

            _logger.LogInformation(
                "Pushed {SuccessCount} prices to Merchant360, {ErrorCount} failed",
                items.Count - result.ErrorCount, result.ErrorCount);

            return new PushResult(items.Count - result.ErrorCount, result.ErrorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push prices to Merchant360 for dealer {DealerId}", dealerId);
            return new PushResult(0, items.Count);
        }
    }

    /// <summary>
    /// Transforms raw price data to canonical format.
    /// </summary>
    public PriceUpdate TransformToCanonical(
        Dictionary<string, string> rawData,
        int dealerId,
        string tradingPartnerCode,
        string sourceDocumentId)
    {
        return new PriceUpdate
        {
            DealerId = dealerId,
            TradingPartnerCode = tradingPartnerCode,
            PartnerSku = rawData.GetValueOrDefault("sku") ?? string.Empty,
            Upc = rawData.GetValueOrDefault("upc"),
            ManufacturerPartNumber = rawData.GetValueOrDefault("mpn"),
            Cost = decimal.TryParse(rawData.GetValueOrDefault("cost"), out var cost) ? cost : 0,
            ListPrice = decimal.TryParse(rawData.GetValueOrDefault("listPrice"), out var list) ? list : null,
            MapPrice = decimal.TryParse(rawData.GetValueOrDefault("mapPrice"), out var map) ? map : null,
            Currency = CurrencyCode.USD,
            EffectiveDate = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            SourceDocumentId = sourceDocumentId,
            Status = CanonicalStatus.Pending
        };
    }
}

/// <summary>
/// Interface for price feed processing.
/// </summary>
public interface IPriceFeedProcessor
{
    Task<PriceFeedProcessResult> ProcessAsync(
        IReadOnlyList<PriceUpdate> priceUpdates,
        int dealerId,
        int documentId,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateAsync(
        PriceUpdate priceUpdate,
        CancellationToken cancellationToken = default);

    Task<PushResult> PushToMerchant360Async(
        IReadOnlyList<PriceUpdate> priceUpdates,
        int dealerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of price feed processing.
/// </summary>
public class PriceFeedProcessResult
{
    public int TotalItems { get; init; }
    public int ValidatedItems { get; init; }
    public int InvalidItems { get; init; }
    public int PushedItems { get; init; }
    public int FailedPushItems { get; init; }
    public IReadOnlyList<PriceUpdateError> ValidationErrors { get; init; } = Array.Empty<PriceUpdateError>();
    public bool Success { get; init; }
}

/// <summary>
/// Price update validation error.
/// </summary>
public record PriceUpdateError(string Sku, string ErrorMessage);

/// <summary>
/// Result of pushing to Merchant360.
/// </summary>
public record PushResult(int SuccessCount, int FailedCount);
