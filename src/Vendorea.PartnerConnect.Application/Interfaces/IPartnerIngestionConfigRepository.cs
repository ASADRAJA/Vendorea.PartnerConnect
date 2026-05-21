using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for partner ingestion configuration.
/// </summary>
public interface IPartnerIngestionConfigRepository
{
    /// <summary>
    /// Gets the configuration for a partner.
    /// </summary>
    Task<PartnerIngestionConfig?> GetByPartnerCodeAsync(string partnerCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the configuration for a partner.
    /// </summary>
    Task SaveAsync(PartnerIngestionConfig config, CancellationToken cancellationToken = default);
}
