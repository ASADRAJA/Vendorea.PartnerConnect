using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IPartnerDistributionCenterRepository
{
    /// <summary>All distribution centers for a trading partner (used to enrich feed DC numbers).</summary>
    Task<IReadOnlyList<PartnerDistributionCenter>> GetByPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>A single distribution center by its surrogate id (tracked, for updates/deletes).</summary>
    Task<PartnerDistributionCenter?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True if the partner already has a DC with this number (optionally excluding one row, for updates).</summary>
    Task<bool> DcNumberExistsAsync(int tradingPartnerId, int dcNumber, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<PartnerDistributionCenter> AddAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default);

    Task UpdateAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default);

    Task DeleteAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default);
}
