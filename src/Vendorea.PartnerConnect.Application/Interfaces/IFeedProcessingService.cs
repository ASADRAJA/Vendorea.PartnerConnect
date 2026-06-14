using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for processing partner feeds (prices, inventory).
/// </summary>
public interface IFeedProcessingService
{
    Task<PriceFeedBatch> ProcessPriceFeedAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<InventoryFeedBatch> ProcessInventoryFeedAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<ContentSyncJob> ProcessContentSyncAsync(int tradingPartnerId, ContentSyncType syncType, CancellationToken cancellationToken = default);
}
