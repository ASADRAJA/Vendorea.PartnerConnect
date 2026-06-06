using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for synchronizing tenants from Merchant360.
/// Fetches merchants from M360 and maintains them locally as tenants under the M360 organization.
/// </summary>
public class TenantSyncService : ITenantSyncService
{
    private const string M360OrganizationCode = "M360";
    private const string M360OrganizationName = "Merchant360";

    private readonly IMerchant360Client _merchant360Client;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantSyncService> _logger;

    public TenantSyncService(
        IMerchant360Client merchant360Client,
        IOrganizationRepository organizationRepository,
        ITenantRepository tenantRepository,
        ILogger<TenantSyncService> logger)
    {
        _merchant360Client = merchant360Client;
        _organizationRepository = organizationRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes tenants from Merchant360 to the local database.
    /// Creates the M360 organization if it doesn't exist, then upserts all merchants as tenants.
    /// </summary>
    public async Task<TenantSyncResult> SyncFromMerchant360Async(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TenantSyncResult();

        try
        {
            _logger.LogInformation("Starting tenant sync from Merchant360");

            // Step 1: Find or create the M360 organization
            var m360Org = await GetOrCreateM360OrganizationAsync(cancellationToken);
            result.OrganizationId = m360Org.Id;
            result.OrganizationCode = m360Org.Code;

            // Step 2: Fetch all merchants from M360 (including inactive to handle deactivations)
            var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: false, cancellationToken);
            result.TotalMerchants = merchants.Count;

            _logger.LogInformation("Fetched {Count} merchants from Merchant360", merchants.Count);

            if (merchants.Count == 0)
            {
                _logger.LogWarning("No merchants returned from Merchant360. Sync complete with no changes.");
                result.Success = true;
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Step 3: Get existing tenants for comparison
            var existingTenants = await _tenantRepository.GetByOrganizationIdAsync(m360Org.Id, cancellationToken);
            var existingByExternalId = existingTenants
                .Where(t => !string.IsNullOrEmpty(t.ExternalId))
                .ToDictionary(t => t.ExternalId!, t => t);

            // Track which external IDs we've seen from M360
            var seenExternalIds = new HashSet<string>();

            // Step 4: Process each merchant
            foreach (var merchant in merchants)
            {
                try
                {
                    var externalId = merchant.Id.ToString();
                    seenExternalIds.Add(externalId);

                    if (existingByExternalId.TryGetValue(externalId, out var existingTenant))
                    {
                        // Update existing tenant
                        var updated = await UpdateTenantAsync(existingTenant, merchant, cancellationToken);
                        if (updated)
                        {
                            result.TenantsUpdated++;
                        }
                    }
                    else
                    {
                        // Create new tenant
                        await CreateTenantAsync(m360Org.Id, merchant, cancellationToken);
                        result.TenantsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Error processing merchant {merchant.Id} ({merchant.Name}): {ex.Message}");
                    _logger.LogError(ex, "Error processing merchant {MerchantId} ({MerchantName})",
                        merchant.Id, merchant.Name);
                }
            }

            // Step 5: Deactivate tenants that no longer exist in M360
            foreach (var existingTenant in existingTenants)
            {
                if (!string.IsNullOrEmpty(existingTenant.ExternalId) &&
                    !seenExternalIds.Contains(existingTenant.ExternalId) &&
                    existingTenant.Status != TenantStatus.Inactive)
                {
                    try
                    {
                        existingTenant.Status = TenantStatus.Inactive;
                        await _tenantRepository.UpdateAsync(existingTenant, cancellationToken);
                        result.TenantsDeactivated++;
                        _logger.LogInformation("Deactivated tenant {TenantId} ({TenantCode}) - no longer in M360",
                            existingTenant.Id, existingTenant.Code);
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Error deactivating tenant {existingTenant.Id}: {ex.Message}");
                        _logger.LogError(ex, "Error deactivating tenant {TenantId}", existingTenant.Id);
                    }
                }
            }

            result.Success = result.Errors == 0;
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Tenant sync completed in {Duration}ms. Created={Created}, Updated={Updated}, Deactivated={Deactivated}, Errors={Errors}",
                stopwatch.ElapsedMilliseconds, result.TenantsCreated, result.TenantsUpdated,
                result.TenantsDeactivated, result.Errors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant sync failed");
            result.Success = false;
            result.Errors++;
            result.ErrorMessages.Add($"Sync failed: {ex.Message}");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }
    }

    private async Task<Organization> GetOrCreateM360OrganizationAsync(CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByCodeAsync(M360OrganizationCode, cancellationToken);

        if (org != null)
        {
            _logger.LogDebug("Found existing M360 organization: Id={OrgId}", org.Id);
            return org;
        }

        // Create the M360 organization
        org = new Organization
        {
            Code = M360OrganizationCode,
            Name = M360OrganizationName,
            IsMultiTenant = true,
            Status = OrganizationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        };

        org = await _organizationRepository.AddAsync(org, cancellationToken);
        _logger.LogInformation("Created M360 organization: Id={OrgId}", org.Id);

        return org;
    }

    private async Task CreateTenantAsync(int organizationId, Merchant360Merchant merchant, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            OrganizationId = organizationId,
            Code = merchant.Code ?? $"M{merchant.Id}",
            Name = merchant.Name,
            ExternalId = merchant.Id.ToString(),
            Status = merchant.IsActive ? TenantStatus.Active : TenantStatus.Inactive,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        _logger.LogInformation("Created tenant {TenantId} ({TenantCode}) for M360 merchant {MerchantId}",
            tenant.Id, tenant.Code, merchant.Id);
    }

    private async Task<bool> UpdateTenantAsync(Tenant tenant, Merchant360Merchant merchant, CancellationToken cancellationToken)
    {
        var needsUpdate = false;

        // Update name if changed
        if (tenant.Name != merchant.Name)
        {
            tenant.Name = merchant.Name;
            needsUpdate = true;
        }

        // Update code if changed
        var newCode = merchant.Code ?? $"M{merchant.Id}";
        if (tenant.Code != newCode)
        {
            tenant.Code = newCode;
            needsUpdate = true;
        }

        // Update status based on M360 active flag
        var newStatus = merchant.IsActive ? TenantStatus.Active : TenantStatus.Inactive;
        if (tenant.Status != newStatus)
        {
            tenant.Status = newStatus;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);
            _logger.LogDebug("Updated tenant {TenantId} ({TenantCode}) from M360 merchant {MerchantId}",
                tenant.Id, tenant.Code, merchant.Id);
        }

        return needsUpdate;
    }
}
