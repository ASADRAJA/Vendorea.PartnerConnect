namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for synchronizing tenants from external sources (e.g., M360).
/// </summary>
public interface ITenantSyncService
{
    /// <summary>
    /// Synchronizes tenants from Merchant360 to the local database.
    /// Creates the M360 organization if it doesn't exist, then upserts all merchants as tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the sync operation.</returns>
    Task<TenantSyncResult> SyncFromMerchant360Async(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a tenant sync operation.
/// </summary>
public class TenantSyncResult
{
    public bool Success { get; set; }
    public int OrganizationId { get; set; }
    public string OrganizationCode { get; set; } = string.Empty;
    public int TotalMerchants { get; set; }
    public int TenantsCreated { get; set; }
    public int TenantsUpdated { get; set; }
    public int TenantsDeactivated { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}
