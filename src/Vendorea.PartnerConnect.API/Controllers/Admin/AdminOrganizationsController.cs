using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for managing organizations.
/// </summary>
[ApiController]
[Route("api/admin/organizations")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<AdminOrganizationsController> _logger;

    public AdminOrganizationsController(
        IOrganizationRepository organizationRepository,
        ITenantRepository tenantRepository,
        ILogger<AdminOrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all organizations with optional status filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrganizations([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        // Get tenant counts for each organization
        var orgDtos = new List<OrganizationDto>();
        foreach (var org in organizations)
        {
            var tenants = await _tenantRepository.GetByOrganizationIdAsync(org.Id, cancellationToken);
            orgDtos.Add(MapToDto(org, tenants.Count));
        }

        return Ok(new OrganizationListResult
        {
            Total = orgDtos.Count,
            ActiveCount = orgDtos.Count(o => o.Status == "Active"),
            SuspendedCount = orgDtos.Count(o => o.Status == "Suspended"),
            PendingCount = orgDtos.Count(o => o.Status == "Pending"),
            Items = orgDtos
        });
    }

    /// <summary>
    /// Gets an organization by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrganization(int id, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        var tenants = await _tenantRepository.GetByOrganizationIdAsync(id, cancellationToken);
        return Ok(MapToDto(org, tenants.Count));
    }

    /// <summary>
    /// Creates a new organization.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        // Check if code already exists
        if (await _organizationRepository.CodeExistsAsync(request.Code, cancellationToken))
        {
            return BadRequest(new { error = "Organization code already exists" });
        }

        var organization = new Organization
        {
            Code = request.Code,
            Name = request.Name,
            BillingPlanId = request.BillingPlanId?.ToString(),
            IsMultiTenant = request.IsMultiTenant,
            Status = OrganizationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _organizationRepository.AddAsync(organization, cancellationToken);

        // If single-tenant, create a default tenant automatically
        if (!request.IsMultiTenant)
        {
            var defaultTenant = new Tenant
            {
                OrganizationId = organization.Id,
                Code = "DEFAULT",
                Name = $"{organization.Name} (Default)",
                Status = TenantStatus.Active,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            };
            await _tenantRepository.AddAsync(defaultTenant, cancellationToken);
        }

        _logger.LogInformation("Created organization {OrgId} ({OrgCode})", organization.Id, organization.Code);

        return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, MapToDto(organization, request.IsMultiTenant ? 0 : 1));
    }

    /// <summary>
    /// Updates an organization.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        org.Name = request.Name;
        if (request.BillingPlanId.HasValue)
            org.BillingPlanId = request.BillingPlanId.Value.ToString();

        await _organizationRepository.UpdateAsync(org, cancellationToken);

        _logger.LogInformation("Updated organization {OrgId}", id);

        return NoContent();
    }

    /// <summary>
    /// Activates an organization.
    /// </summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> ActivateOrganization(int id, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        if (org.Status == OrganizationStatus.Active)
            return BadRequest(new { error = "Organization is already active" });

        org.Status = OrganizationStatus.Active;
        org.ActivatedAt = DateTime.UtcNow;
        org.SuspendedAt = null;
        org.SuspensionReason = null;

        await _organizationRepository.UpdateAsync(org, cancellationToken);

        _logger.LogInformation("Activated organization {OrgId}", id);

        return NoContent();
    }

    /// <summary>
    /// Suspends an organization.
    /// </summary>
    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> SuspendOrganization(int id, [FromBody] SuspendRequest? request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        if (org.Status == OrganizationStatus.Suspended)
            return BadRequest(new { error = "Organization is already suspended" });

        org.Status = OrganizationStatus.Suspended;
        org.SuspendedAt = DateTime.UtcNow;
        org.SuspensionReason = request?.Reason;

        await _organizationRepository.UpdateAsync(org, cancellationToken);

        _logger.LogInformation("Suspended organization {OrgId}", id);

        return NoContent();
    }

    private static OrganizationDto MapToDto(Organization org, int tenantCount)
    {
        return new OrganizationDto
        {
            Id = org.Id,
            Code = org.Code,
            Name = org.Name,
            Status = org.Status.ToString(),
            BillingPlanId = Guid.TryParse(org.BillingPlanId, out var planId) ? planId : null,
            IsMultiTenant = org.IsMultiTenant,
            TenantCount = tenantCount,
            CreatedAt = org.CreatedAt,
            ActivatedAt = org.ActivatedAt,
            SuspendedAt = org.SuspendedAt
        };
    }
}

public class OrganizationDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? BillingPlanId { get; set; }
    public string? BillingPlanName { get; set; }
    public bool IsMultiTenant { get; set; }
    public int TenantCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
}

public class OrganizationListResult
{
    public int Total { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public int PendingCount { get; set; }
    public List<OrganizationDto> Items { get; set; } = new();
}

public class CreateOrganizationRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? BillingPlanId { get; set; }
    public bool IsMultiTenant { get; set; }
}

public class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? BillingPlanId { get; set; }
}

public class SuspendRequest
{
    public string? Reason { get; set; }
}
