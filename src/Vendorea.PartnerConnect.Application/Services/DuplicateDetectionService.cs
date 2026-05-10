using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Options for duplicate detection.
/// </summary>
public class DuplicateDetectionOptions
{
    public const string SectionName = "DuplicateDetection";

    /// <summary>
    /// Default retention period in days for fingerprints.
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 90;

    /// <summary>
    /// Whether to enable duplicate detection globally.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Service for detecting duplicate documents using content hashing.
/// </summary>
public class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly IDocumentFingerprintRepository _repository;
    private readonly DuplicateDetectionOptions _options;
    private readonly ILogger<DuplicateDetectionService> _logger;

    public DuplicateDetectionService(
        IDocumentFingerprintRepository repository,
        IOptions<DuplicateDetectionOptions> options,
        ILogger<DuplicateDetectionService> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        return await _repository.ExistsAsync(
            dealerPartnerConnectionId,
            documentType,
            contentHash,
            cancellationToken);
    }

    public async Task<DuplicateCheckResult> CheckDuplicateAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return DuplicateCheckResult.NotDuplicate;
        }

        var existing = await _repository.FindByHashAsync(
            dealerPartnerConnectionId,
            documentType,
            contentHash,
            cancellationToken);

        if (existing == null)
        {
            return DuplicateCheckResult.NotDuplicate;
        }

        _logger.LogInformation(
            "Duplicate document detected for connection {ConnectionId}, type {DocumentType}, " +
            "hash {Hash}. Original document: {OriginalId}",
            dealerPartnerConnectionId, documentType, contentHash[..16], existing.OriginalDocumentId);

        return DuplicateCheckResult.Duplicate(existing);
    }

    public async Task RegisterFingerprintAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        int originalDocumentId,
        string? fileName = null,
        long? fileSizeBytes = null,
        int? retentionDays = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var effectiveRetentionDays = retentionDays ?? _options.DefaultRetentionDays;

        var fingerprint = new DocumentFingerprint
        {
            DealerPartnerConnectionId = dealerPartnerConnectionId,
            DocumentType = documentType,
            ContentHash = contentHash,
            OriginalDocumentId = originalDocumentId,
            OriginalFileName = fileName,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = effectiveRetentionDays > 0
                ? DateTime.UtcNow.AddDays(effectiveRetentionDays)
                : null
        };

        try
        {
            await _repository.AddAsync(fingerprint, cancellationToken);

            _logger.LogDebug(
                "Registered fingerprint for document {DocumentId}, type {DocumentType}, hash {Hash}",
                originalDocumentId, documentType, contentHash[..16]);
        }
        catch (Exception ex) when (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            // Fingerprint already exists - this is fine, just log it
            _logger.LogDebug(
                "Fingerprint already exists for connection {ConnectionId}, type {DocumentType}, hash {Hash}",
                dealerPartnerConnectionId, documentType, contentHash[..16]);
        }
    }

    public async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);

        // Reset stream position for subsequent reads
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ComputeHash(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow;
        var deletedCount = await _repository.DeleteExpiredAsync(cutoffDate, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired document fingerprints", deletedCount);
        }

        return deletedCount;
    }
}
