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
    /// Processes a previously queued (Pending) upload: claims it, loads the stored file, parses
    /// and inserts the records, and sets the final status. Invoked by the background worker.
    /// No-op (returns a result with Success=false) if the upload was already claimed or is gone.
    /// </summary>
    Task<PriceFeedUploadResult> ProcessPendingUploadAsync(
        int uploadId,
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
    /// Gets all upload history (admin).
    /// </summary>
    Task<IReadOnlyList<PriceFeedUploadDto>> GetAllUploadHistoryAsync(
        int? dealerId = null,
        string? tradingPartnerCode = null,
        int limit = 100,
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

    /// <summary>Cancels a queued (Pending) upload so it will not be processed.</summary>
    Task<PriceFeedActionResult> CancelUploadAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an upload, its price records, and its stored file. Not allowed while Processing.</summary>
    Task<PriceFeedActionResult> DeleteUploadAsync(int uploadId, CancellationToken cancellationToken = default);

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
    bool IsDuplicate = false,
    string? Status = null);

/// <summary>Outcome of a cancel/delete action, mapped to HTTP status by the controller.</summary>
public enum PriceFeedActionStatus { Ok, NotFound, Conflict }

public record PriceFeedActionResult(PriceFeedActionStatus Status, string? Message = null);

/// <summary>
/// Result of pushing to Merchant360.
/// </summary>
public record PushToMerchant360Result(
    bool Success,
    int RecordsPushed,
    string? ErrorMessage = null,
    int RecordsReceived = 0,
    int RecordsCreated = 0,
    int RecordsUpdated = 0,
    int RecordsSkipped = 0);

/// <summary>
/// DTO for price feed upload summary.
/// </summary>
public record PriceFeedUploadDto(
    int Id,
    int DealerId,
    string? DealerName,
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
