using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;

/// <summary>
/// DTO for dealer-partner connection information.
/// </summary>
public record DealerConnectionDto(
    int Id,
    int DealerId,
    int TradingPartnerId,
    string? TradingPartnerCode,
    string? TradingPartnerName,
    string? ExternalAccountId,
    ConnectionStatus Status,
    DateTime? LastSyncAt,
    DateTime? LastSuccessfulSyncAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// DTO for connection statistics.
/// </summary>
public record ConnectionStatisticsDto(
    int TotalDocumentsProcessed,
    int TotalPriceFeedBatches,
    int TotalInventoryFeedBatches,
    int TotalContentSyncJobs,
    int TotalErrors,
    DateTime? LastSuccessfulSync,
    decimal SuccessRate);

/// <summary>
/// Command to update a dealer connection.
/// </summary>
public record UpdateDealerConnectionCommand(
    string? ExternalAccountId,
    string? ConfigurationJson,
    ConnectionStatus? Status);

/// <summary>
/// Command to update connection credentials.
/// </summary>
public record UpdateConnectionCredentialsCommand(
    string CredentialsJson);

/// <summary>
/// Response for testing a connection.
/// </summary>
public record TestConnectionResponse(
    bool Success,
    string? ErrorMessage,
    DateTime TestedAt);
