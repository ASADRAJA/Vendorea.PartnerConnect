using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;

/// <summary>
/// DTO for partner document information.
/// </summary>
public record DocumentDto(
    int Id,
    int DealerPartnerConnectionId,
    int DealerId,
    string? TradingPartnerCode,
    DocumentType DocumentType,
    DocumentDirection Direction,
    DocumentStatus Status,
    string? FileName,
    string? ContentType,
    long? FileSizeBytes,
    int? RecordCount,
    int? ProcessedCount,
    int? ErrorCount,
    DateTime? ReceivedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    DateTime? SentAt,
    string? ErrorMessage);

/// <summary>
/// DTO for paginated document list.
/// </summary>
public record DocumentListResponse(
    IReadOnlyList<DocumentDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage);

/// <summary>
/// Query parameters for listing documents.
/// </summary>
public record DocumentListQuery(
    int? DealerId = null,
    int? ConnectionId = null,
    DocumentType? DocumentType = null,
    DocumentStatus? Status = null,
    DocumentDirection? Direction = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 20);
