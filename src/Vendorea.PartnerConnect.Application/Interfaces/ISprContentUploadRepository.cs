using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing SPR content upload tracking.
/// Content uploads are SHARED - not dealer-specific.
/// </summary>
public interface ISprContentUploadRepository
{
    /// <summary>
    /// Gets a content upload by ID.
    /// </summary>
    Task<SprContentUpload?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all content uploads.
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetAllAsync(
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest completed content upload for a locale.
    /// </summary>
    Task<SprContentUpload?> GetLatestCompletedAsync(
        string localeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content uploads by status.
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetByStatusAsync(
        ContentUploadStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending or in-progress uploads that may need processing.
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetPendingUploadsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a content version already exists.
    /// </summary>
    Task<bool> ExistsByVersionAsync(
        string contentVersion,
        string localeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file with the same hash already exists.
    /// </summary>
    Task<SprContentUpload?> GetByFileHashAsync(
        string zipFileHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new content upload record.
    /// </summary>
    Task<SprContentUpload> CreateAsync(
        SprContentUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing content upload record.
    /// </summary>
    Task UpdateAsync(
        SprContentUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the processing progress for an upload.
    /// </summary>
    Task UpdateProgressAsync(
        int uploadId,
        int processedProducts,
        int? errorProducts = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an upload as completed.
    /// </summary>
    Task MarkCompletedAsync(
        int uploadId,
        int totalProducts,
        int processedProducts,
        int errorProducts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an upload as failed.
    /// </summary>
    Task MarkFailedAsync(
        int uploadId,
        string errorDetails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a content upload and all associated content.
    /// </summary>
    Task DeleteAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upload history.
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetUploadHistoryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets uploads by locale.
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetByLocaleAsync(
        string localeId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there's any completed content data for a trading partner.
    /// </summary>
    Task<bool> HasDataForPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an upload as pushed to Merchant360.
    /// </summary>
    Task MarkPushedToM360Async(int uploadId, CancellationToken cancellationToken = default);

    // --- Durable Merchant360 push queue ---

    /// <summary>
    /// Gets uploads whose Merchant360 push is in the given queue status (e.g. "Queued").
    /// </summary>
    Task<IReadOnlyList<SprContentUpload>> GetByM360PushStatusAsync(
        string status,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a queued push (Queued -> Pushing), stamping the claim time and clearing any
    /// prior error. Returns true only for the single worker that won the claim.
    /// </summary>
    Task<bool> TryClaimM360PushAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues an upload for a Merchant360 push (status -> "Queued") and zeroes the progress counters,
    /// but only when it is not already Queued or Pushing. Returns whether it was enqueued.
    /// </summary>
    Task<bool> TryEnqueueM360PushAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight per-page progress update of just the push counter columns (no entity tracking).
    /// </summary>
    Task UpdateM360PushProgressAsync(
        int uploadId,
        int productsPushed,
        int currentBatch,
        int totalBatches,
        int totalProducts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a push as completed (status -> "Pushed", stamps PushedToM360At, clears the error).
    /// </summary>
    Task MarkM360PushCompletedAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a push as failed (status -> "Failed", records the error).
    /// </summary>
    Task MarkM360PushFailedAsync(int uploadId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns pushes stranded in "Pushing" (claimed before the cutoff) back to a terminal "Failed"
    /// state so an operator can re-trigger. Returns the count reclaimed.
    /// </summary>
    Task<int> ReclaimStaleM360PushAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}
