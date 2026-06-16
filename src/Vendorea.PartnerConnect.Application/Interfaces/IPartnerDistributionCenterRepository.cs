using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IPartnerDistributionCenterRepository
{
    /// <summary>All distribution centers for a trading partner (used to enrich feed DC numbers).</summary>
    Task<IReadOnlyList<PartnerDistributionCenter>> GetByPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
}
