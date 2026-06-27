using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing price feed uploads across all suppliers.
/// </summary>
public interface IPriceFeedUploadRepository
{
    /// <summary>
    /// Gets an upload by ID.
    /// </summary>
    Task<PriceFeedUpload?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all uploads for a specific dealer.
    /// </summary>
    Task<IReadOnlyList<PriceFeedUpload>> GetByDealerIdAsync(
        int dealerId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all uploads for a dealer from a specific trading partner.
    /// </summary>
    Task<IReadOnlyList<PriceFeedUpload>> GetByDealerAndPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent upload for a dealer from a specific trading partner.
    /// </summary>
    Task<PriceFeedUpload?> GetLatestAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a prior <em>successful</em> upload (Completed or pushed, with at least one record)
    /// of the same file content for this dealer/partner, or null if none. Failed or empty
    /// attempts are ignored so the same file can be retried after a failure.
    /// </summary>
    Task<PriceFeedUpload?> GetSuccessfulUploadByHashAsync(
        int dealerId,
        int tradingPartnerId,
        string fileHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new upload.
    /// </summary>
    Task<PriceFeedUpload> AddAsync(PriceFeedUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing upload.
    /// </summary>
    Task UpdateAsync(PriceFeedUpload upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets uploads by status.
    /// </summary>
    Task<IReadOnlyList<PriceFeedUpload>> GetByStatusAsync(
        PriceFeedUploadStatus status,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all uploads with optional filters (admin).
    /// </summary>
    Task<IReadOnlyList<PriceFeedUpload>> GetAllAsync(
        int? dealerId = null,
        int? tradingPartnerId = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there's any completed price data for a trading partner.
    /// </summary>
    Task<bool> HasDataForPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
}
