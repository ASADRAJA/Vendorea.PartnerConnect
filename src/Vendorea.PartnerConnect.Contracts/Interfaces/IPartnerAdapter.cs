using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Base interface for partner-specific adapters.
/// Each trading partner implementation will implement this interface.
/// </summary>
public interface IPartnerAdapter
{
    /// <summary>
    /// The unique code identifying this partner adapter.
    /// </summary>
    string PartnerCode { get; }

    /// <summary>
    /// Tests connectivity to the partner system.
    /// </summary>
    Task<bool> TestConnectionAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of capabilities supported by this adapter.
    /// </summary>
    IReadOnlyList<PartnerCapability> SupportedCapabilities { get; }
}

/// <summary>
/// Interface for adapters that support price feed retrieval.
/// </summary>
public interface IPriceFeedAdapter : IPartnerAdapter
{
    Task<PriceFeedResult> FetchPriceFeedAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for adapters that support inventory feed retrieval.
/// </summary>
public interface IInventoryFeedAdapter : IPartnerAdapter
{
    Task<InventoryFeedResult> FetchInventoryFeedAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a price feed fetch operation.
/// </summary>
public record PriceFeedResult(
    bool Success,
    string? FilePath,
    int? RecordCount,
    string? ErrorMessage);

/// <summary>
/// Result of an inventory feed fetch operation.
/// </summary>
public record InventoryFeedResult(
    bool Success,
    string? FilePath,
    int? RecordCount,
    string? ErrorMessage);
