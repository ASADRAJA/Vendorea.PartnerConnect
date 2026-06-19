using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for managing tenants.
/// </summary>
[ApiController]
[Route("api/admin/tenants")]
public class AdminTenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantPartnerAccountRepository _accountRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ILogger<AdminTenantsController> _logger;

    public AdminTenantsController(
        ITenantRepository tenantRepository,
        IOrganizationRepository organizationRepository,
        ITenantPartnerAccountRepository accountRepository,
        ITradingPartnerRepository partnerRepository,
        ILogger<AdminTenantsController> logger)
    {
        _tenantRepository = tenantRepository;
        _organizationRepository = organizationRepository;
        _accountRepository = accountRepository;
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all tenants with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTenants(
        [FromQuery] int? organizationId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Tenant> tenants;

        if (organizationId.HasValue)
        {
            tenants = await _tenantRepository.GetByOrganizationIdAsync(organizationId.Value, cancellationToken);
        }
        else
        {
            tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        }

        // Attach each tenant's organization (name + status) so the DTO can report effective status.
        var orgs = (await _organizationRepository.GetAllAsync(cancellationToken)).ToDictionary(o => o.Id);
        foreach (var tenant in tenants)
        {
            if (orgs.TryGetValue(tenant.OrganizationId, out var org))
                tenant.Organization = org;
        }

        var tenantDtos = new List<TenantDto>();
        foreach (var tenant in tenants)
        {
            var accounts = await _accountRepository.GetByTenantIdAsync(tenant.Id, cancellationToken);
            tenantDtos.Add(MapToDto(tenant, accounts.Count));
        }

        return Ok(new TenantListResult
        {
            Total = tenantDtos.Count,
            ActiveCount = tenantDtos.Count(t => t.Status == "Active"),
            SuspendedCount = tenantDtos.Count(t => t.Status == "Suspended"),
            Items = tenantDtos
        });
    }

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTenant(int id, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdWithOrganizationAsync(id, cancellationToken);
        if (tenant == null)
            return NotFound();

        var accounts = await _accountRepository.GetByTenantIdAsync(id, cancellationToken);
        return Ok(MapToDto(tenant, accounts.Count));
    }

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        // Validate organization exists
        var org = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (org == null)
            return BadRequest(new { error = "Organization not found" });

        // Check if code already exists in this organization
        var existing = await _tenantRepository.GetByCodeAsync(request.OrganizationId, request.Code, cancellationToken);
        if (existing != null)
            return BadRequest(new { error = "Tenant code already exists in this organization" });

        var tenant = new Tenant
        {
            OrganizationId = request.OrganizationId,
            Code = request.Code,
            Name = request.Name,
            ExternalId = request.ExternalId,
            IsDefault = request.IsDefault,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        tenant.Organization = org;

        _logger.LogInformation("Created tenant {TenantId} ({TenantCode}) in organization {OrgId}", tenant.Id, tenant.Code, org.Id);

        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, MapToDto(tenant, 0));
    }

    /// <summary>
    /// Updates a tenant.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTenant(int id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
            return NotFound();

        tenant.Name = request.Name;
        tenant.ExternalId = request.ExternalId;

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Updated tenant {TenantId}", id);

        return NoContent();
    }

    /// <summary>
    /// Activates a tenant.
    /// </summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> ActivateTenant(int id, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
            return NotFound();

        if (tenant.Status == TenantStatus.Active)
            return BadRequest(new { error = "Tenant is already active" });

        // Guard the effective-status invariant: a tenant can only be active under an active org.
        var org = await _organizationRepository.GetByIdAsync(tenant.OrganizationId, cancellationToken);
        if (org is null)
            return BadRequest(new { error = "Organization not found" });
        if (!EffectiveStatus.IsOrganizationActive(org))
            return BadRequest(new { error = "Cannot activate a tenant while its organization is not active" });

        tenant.Status = TenantStatus.Active;
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Activated tenant {TenantId}", id);

        return NoContent();
    }

    /// <summary>
    /// Suspends a tenant.
    /// </summary>
    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> SuspendTenant(int id, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
            return NotFound();

        if (tenant.Status == TenantStatus.Suspended)
            return BadRequest(new { error = "Tenant is already suspended" });

        tenant.Status = TenantStatus.Suspended;
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        _logger.LogInformation("Suspended tenant {TenantId}", id);

        return NoContent();
    }

    /// <summary>
    /// Gets partner accounts for a tenant.
    /// </summary>
    [HttpGet("{tenantId:int}/accounts")]
    public async Task<IActionResult> GetTenantPartnerAccounts(int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return NotFound();

        var accounts = await _accountRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        var accountDtos = new List<TenantPartnerAccountDto>();
        foreach (var account in accounts)
        {
            var partner = await _partnerRepository.GetByIdAsync(account.TradingPartnerId, cancellationToken);
            accountDtos.Add(new TenantPartnerAccountDto
            {
                Id = account.Id,
                TenantId = account.TenantId ?? tenantId,
                TenantName = tenant.Name,
                TradingPartnerId = account.TradingPartnerId,
                TradingPartnerCode = partner?.Code,
                TradingPartnerName = partner?.Name,
                AccountNumber = account.AccountNumber,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt
            });
        }

        return Ok(accountDtos);
    }

    /// <summary>
    /// Creates a partner account for a tenant.
    /// </summary>
    [HttpPost("{tenantId:int}/accounts")]
    public async Task<IActionResult> CreateTenantPartnerAccount(
        int tenantId,
        [FromBody] CreateTenantPartnerAccountRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return NotFound(new { error = "Tenant not found" });

        // Validate trading partner exists
        var partner = await _partnerRepository.GetByIdAsync(request.TradingPartnerId, cancellationToken);
        if (partner == null)
            return BadRequest(new { error = "Trading partner not found" });

        // Check if account already exists
        if (await _accountRepository.ExistsAsync(tenantId, request.TradingPartnerId, request.AccountNumber, cancellationToken))
            return BadRequest(new { error = "Account already exists for this tenant and trading partner" });

        var account = new TenantPartnerAccount
        {
            TenantId = tenantId,
            TradingPartnerId = request.TradingPartnerId,
            AccountNumber = request.AccountNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _accountRepository.AddAsync(account, cancellationToken);

        _logger.LogInformation("Created partner account {AccountId} for tenant {TenantId} with partner {PartnerId}",
            account.Id, tenantId, request.TradingPartnerId);

        return CreatedAtAction(nameof(GetTenantPartnerAccounts), new { tenantId }, new TenantPartnerAccountDto
        {
            Id = account.Id,
            TenantId = account.TenantId ?? tenantId,
            TenantName = tenant.Name,
            TradingPartnerId = account.TradingPartnerId,
            TradingPartnerCode = partner.Code,
            TradingPartnerName = partner.Name,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt
        });
    }

    /// <summary>
    /// Deactivates a partner account.
    /// </summary>
    [HttpPost("/api/admin/accounts/{accountId:int}/deactivate")]
    public async Task<IActionResult> DeactivatePartnerAccount(int accountId, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account == null)
            return NotFound();

        account.IsActive = false;
        await _accountRepository.UpdateAsync(account, cancellationToken);

        _logger.LogInformation("Deactivated partner account {AccountId}", accountId);

        return NoContent();
    }

    /// <summary>
    /// Lists merchants (tenants) that have active, approved partner connections, grouped by tenant.
    /// Replaces the legacy subscription-based endpoint; used by the price-feed and content pages to
    /// filter their merchant/partner dropdowns. Built from TenantPartnerAccount connections.
    /// </summary>
    [HttpGet("active-by-merchant")]
    public async Task<IActionResult> GetActiveByMerchant(CancellationToken cancellationToken)
    {
        var approved = await _accountRepository.GetConnectionsAsync(
            organizationId: null, status: ConnectionApprovalStatus.Approved, cancellationToken);

        var result = approved
            .Where(c => c.IsActive && c.TenantId != null)
            .GroupBy(c => c.TenantId!.Value)
            .Select(g =>
            {
                var first = g.First();
                return new MerchantWithConnectionsDto
                {
                    MerchantId = g.Key,
                    MerchantName = first.Tenant?.Name ?? $"Merchant {g.Key}",
                    MerchantCode = first.Tenant?.Code ?? $"M{g.Key}",
                    Partners = g.Select(c => new ConnectedPartnerDto
                    {
                        TradingPartnerId = c.TradingPartnerId,
                        TradingPartnerCode = c.TradingPartner?.Code ?? string.Empty,
                        TradingPartnerName = c.TradingPartner?.Name ?? $"Partner {c.TradingPartnerId}",
                        AccountNumber = c.AccountNumber
                    }).ToList()
                };
            })
            .OrderBy(m => m.MerchantName)
            .ToList();

        return Ok(result);
    }

    private static TenantDto MapToDto(Tenant tenant, int accountCount)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            OrganizationId = tenant.OrganizationId,
            OrganizationName = tenant.Organization?.Name,
            Code = tenant.Code,
            Name = tenant.Name,
            ExternalId = tenant.ExternalId,
            Status = tenant.Status.ToString(),
            OrganizationStatus = tenant.Organization?.Status.ToString() ?? string.Empty,
            IsDefault = tenant.IsDefault,
            PartnerAccountCount = accountCount,
            CreatedAt = tenant.CreatedAt
        };
    }
}

/// <summary>A merchant (tenant) with its active partner connections (replaces the subscription DTO).</summary>
public class MerchantWithConnectionsDto
{
    public int MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantCode { get; set; } = string.Empty;
    public List<ConnectedPartnerDto> Partners { get; set; } = new();
}

public class ConnectedPartnerDto
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string TradingPartnerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class TenantDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>The parent organization's status — a tenant is only effectively active under an Active org.</summary>
    public string OrganizationStatus { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int PartnerAccountCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenantListResult
{
    public int Total { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public List<TenantDto> Items { get; set; } = new();
}

public class CreateTenantRequest
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
}

public class TenantPartnerAccountDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTenantPartnerAccountRequest
{
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
}
