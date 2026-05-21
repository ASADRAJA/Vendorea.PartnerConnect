using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for importing SPR enhanced content from zip archives.
/// </summary>
public interface ISprContentImportService
{
    /// <summary>
    /// Imports content from a zip file stream.
    /// Content is SHARED MASTER DATA - not dealer-specific.
    /// </summary>
    /// <param name="tradingPartnerId">The trading partner ID (SPR).</param>
    /// <param name="zipStream">The zip file stream.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentVersion">The content version string.</param>
    /// <param name="localeId">The locale (e.g., EN_US).</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created content upload record.</returns>
    Task<SprContentUpload> ImportFromZipAsync(
        int tradingPartnerId,
        Stream zipStream,
        string fileName,
        string contentVersion,
        string localeId,
        Action<ContentImportProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a failed or partially completed import.
    /// </summary>
    Task<SprContentUpload> ResumeImportAsync(
        int uploadId,
        Action<ContentImportProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-progress import.
    /// </summary>
    Task CancelImportAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a zip file without importing.
    /// </summary>
    Task<ContentValidationResult> ValidateZipAsync(
        Stream zipStream,
        string localeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all content for a specific upload.
    /// </summary>
    Task DeleteContentByUploadAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current import status.
    /// </summary>
    Task<ContentImportProgress?> GetImportStatusAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes imported content to Merchant360.
    /// Content is shared across all merchants.
    /// </summary>
    /// <param name="uploadId">The content upload ID to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the push operation.</returns>
    Task<ContentPushResult> PushToMerchant360Async(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes SPR categories to Merchant360.
    /// Categories should be pushed before content to ensure proper FK relationships.
    /// </summary>
    /// <param name="tradingPartnerId">The trading partner ID (SPR).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the category push operation.</returns>
    Task<CategoryPushResult> PushCategoriesToMerchant360Async(int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes both categories and content to Merchant360 in the correct order.
    /// </summary>
    /// <param name="uploadId">The content upload ID to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined result of the push operations.</returns>
    Task<FullContentPushResult> PushAllToMerchant360Async(int uploadId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pushing content to Merchant360.
/// </summary>
public class ContentPushResult
{
    public bool Success { get; set; }
    public int UploadId { get; set; }
    public int RecordsPushed { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int SpecificationsPushed { get; set; }
    public int FeaturesPushed { get; set; }
    public int RelationshipsPushed { get; set; }
    public int BatchCount { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of pushing categories to Merchant360.
/// </summary>
public class CategoryPushResult
{
    public bool Success { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public int CategoriesPushed { get; set; }
    public int CategoriesCreated { get; set; }
    public int CategoriesUpdated { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Combined result of pushing all content (categories + products) to Merchant360.
/// </summary>
public class FullContentPushResult
{
    public bool Success { get; set; }
    public int UploadId { get; set; }
    public CategoryPushResult? CategoryResult { get; set; }
    public ContentPushResult? ContentResult { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Progress information for content import.
/// </summary>
public class ContentImportProgress
{
    public int UploadId { get; set; }
    public ContentUploadStatus Status { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public int TotalFeatures { get; set; }
    public int ProcessedFeatures { get; set; }
    public int TotalRelationships { get; set; }
    public int ProcessedRelationships { get; set; }
    public double PercentComplete => TotalProducts > 0 ? (double)ProcessedProducts / TotalProducts * 100 : 0;
    public string? CurrentFile { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of zip file validation.
/// </summary>
public class ContentValidationResult
{
    public bool IsValid { get; set; }
    public int ProductCount { get; set; }
    public int FeatureCount { get; set; }
    public int RelationshipCount { get; set; }
    public int CategoryCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> AvailableFiles { get; set; } = new();
    public string? DetectedLocale { get; set; }
    public string? DetectedVersion { get; set; }
}
