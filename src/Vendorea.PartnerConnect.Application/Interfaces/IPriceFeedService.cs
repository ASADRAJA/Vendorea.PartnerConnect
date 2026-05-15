using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for managing price feed uploads and processing.
/// </summary>
public interface IPriceFeedService
{
    /// <summary>
    /// Uploads and processes a price feed file for a dealer.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="tradingPartnerCode">The trading partner code (e.g., "SPR").</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="fileStream">The file content stream.</param>
    /// <param name="uploadedByUserId">User who uploaded the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upload result.</returns>
    Task<PriceFeedUploadResult> UploadAsync(
        int dealerId,
        string tradingPartnerCode,
        string fileName,
        Stream fileStream,
        string? uploadedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upload history for a dealer.
    /// </summary>
    Task<IReadOnlyList<PriceFeedUploadDto>> GetUploadHistoryAsync(
        int dealerId,
        string? tradingPartnerCode = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets details of a specific upload.
    /// </summary>
    Task<PriceFeedUploadDetailDto?> GetUploadDetailsAsync(
        int uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the latest price data for a dealer to Merchant360.
    /// </summary>
    Task<PushToMerchant360Result> PushToMerchant360Async(
        int uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current prices for a dealer from a trading partner.
    /// </summary>
    Task<IReadOnlyList<PriceRecordDto>> GetCurrentPricesAsync(
        int dealerId,
        string tradingPartnerCode,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches prices by SKU or description.
    /// </summary>
    Task<IReadOnlyList<PriceRecordDto>> SearchPricesAsync(
        int dealerId,
        string tradingPartnerCode,
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a price feed upload operation.
/// </summary>
public record PriceFeedUploadResult(
    bool Success,
    int UploadId,
    int RecordCount,
    int ErrorCount,
    string? ErrorMessage = null,
    bool IsDuplicate = false);

/// <summary>
/// Result of pushing to Merchant360.
/// </summary>
public record PushToMerchant360Result(
    bool Success,
    int RecordsPushed,
    string? ErrorMessage = null);

/// <summary>
/// DTO for price feed upload summary.
/// </summary>
public record PriceFeedUploadDto(
    int Id,
    int DealerId,
    string TradingPartnerCode,
    string TradingPartnerName,
    string FileName,
    PriceFeedUploadStatus Status,
    int RecordCount,
    int ErrorCount,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    DateTime? PushedToMerchant360At);

/// <summary>
/// DTO for price feed upload details.
/// </summary>
public record PriceFeedUploadDetailDto(
    int Id,
    int DealerId,
    string TradingPartnerCode,
    string TradingPartnerName,
    string FileName,
    string FileHash,
    long FileSizeBytes,
    PriceFeedUploadStatus Status,
    int RecordCount,
    int ErrorCount,
    string? ErrorMessage,
    DateTime UploadedAt,
    string? UploadedByUserId,
    DateTime? ProcessedAt,
    DateTime? PushedToMerchant360At,
    string CorrelationId);

/// <summary>
/// DTO for a price record (generic across suppliers).
/// </summary>
public record PriceRecordDto(
    string PartnerSku,
    string? Upc,
    string Description,
    decimal Cost,
    decimal ListPrice,
    string? CategoryCode,
    string UnitOfMeasure,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate);
