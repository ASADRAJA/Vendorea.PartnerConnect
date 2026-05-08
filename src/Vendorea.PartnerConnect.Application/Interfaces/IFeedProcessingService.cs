using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for processing partner feeds (prices, inventory).
/// </summary>
public interface IFeedProcessingService
{
    Task<PriceFeedBatch> ProcessPriceFeedAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<InventoryFeedBatch> ProcessInventoryFeedAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<ContentSyncJob> ProcessContentSyncAsync(int connectionId, ContentSyncType syncType, CancellationToken cancellationToken = default);
}
