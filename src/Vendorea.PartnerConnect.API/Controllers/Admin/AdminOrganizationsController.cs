using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Security;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for registering and managing organizations.
/// Tenant provisioning is handled by the connections workflow (not here).
/// </summary>
[ApiController]
[Route("api/admin/organizations")]
public class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IBillingPlanRepository _billingPlanRepository;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<AdminOrganizationsController> _logger;

    public AdminOrganizationsController(
        IOrganizationRepository organizationRepository,
        ITenantRepository tenantRepository,
        ITradingPartnerRepository partnerRepository,
        IBillingPlanRepository billingPlanRepository,
        ICredentialProtector credentialProtector,
        ILogger<AdminOrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _tenantRepository = tenantRepository;
        _partnerRepository = partnerRepository;
        _billingPlanRepository = billingPlanRepository;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    /// <summary>
    /// Gets all organizations with status counts.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrganizations([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

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
    /// Gets an organization by ID, including its selected partners.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrganization(int id, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdWithPartnersAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        var tenants = await _tenantRepository.GetByOrganizationIdAsync(id, cancellationToken);
        return Ok(MapToDto(org, tenants.Count));
    }

    /// <summary>
    /// Lists active billing plans (for the registration form's plan selector).
    /// </summary>
    [HttpGet("billing-plans")]
    public async Task<IActionResult> GetBillingPlans(CancellationToken cancellationToken)
    {
        var plans = await _billingPlanRepository.GetAllAsync(includeInactive: false, cancellationToken);
        return Ok(plans.Select(p => new BillingPlanOptionDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            MonthlyPriceCents = p.MonthlyPriceCents,
            Currency = p.Currency
        }).ToList());
    }

    /// <summary>
    /// Registers a new organization (status = Pending). The org code is system-generated.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Organization name is required" });

        // Validate selected partners exist.
        var partnerIds = (request.TradingPartnerIds ?? new List<int>()).Distinct().ToList();
        foreach (var pid in partnerIds)
        {
            if (await _partnerRepository.GetByIdAsync(pid, cancellationToken) is null)
                return BadRequest(new { error = $"Trading partner {pid} not found" });
        }

        if (request.ExternalPortalEnabled && string.IsNullOrWhiteSpace(request.PortalBaseUrl))
            return BadRequest(new { error = "Portal base URL is required when the external portal is enabled" });

        var organization = new Organization
        {
            Code = await _organizationRepository.GenerateNextCodeAsync(cancellationToken),
            Name = request.Name,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            BillingPlanId = request.BillingPlanId?.ToString(),
            PaymentTerms = ParsePaymentTerms(request.PaymentTerms),
            IsMultiTenant = request.IsMultiTenant,
            ExternalPortalEnabled = request.ExternalPortalEnabled,
            PortalBaseUrl = request.ExternalPortalEnabled ? request.PortalBaseUrl : null,
            PortalApiKey = request.ExternalPortalEnabled ? _credentialProtector.Protect(request.PortalApiKey) : null,
            // Hash of the plaintext key for inbound org-facing auth (set alongside the encrypted key).
            PortalApiKeyHash = request.ExternalPortalEnabled ? ApiKeyHasher.Hash(request.PortalApiKey) : null,
            Status = OrganizationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _organizationRepository.AddAsync(organization, cancellationToken);

        if (partnerIds.Count > 0)
            await _organizationRepository.ReplacePartnersAsync(organization.Id, partnerIds, cancellationToken);

        // Tenant provisioning intentionally deferred to the connections workflow (no auto-tenant here).

        _logger.LogInformation("Registered organization {OrgId} ({OrgCode}) in Pending", organization.Id, organization.Code);

        var created = await _organizationRepository.GetByIdWithPartnersAsync(organization.Id, cancellationToken);
        return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, MapToDto(created!, 0));
    }

    /// <summary>
    /// Updates an organization's editable fields (and partner selection / portal details).
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            org.Name = request.Name;
        org.ContactEmail = request.ContactEmail ?? org.ContactEmail;
        org.ContactPhone = request.ContactPhone ?? org.ContactPhone;
        if (request.BillingPlanId.HasValue)
            org.BillingPlanId = request.BillingPlanId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(request.PaymentTerms))
            org.PaymentTerms = ParsePaymentTerms(request.PaymentTerms);

        org.ExternalPortalEnabled = request.ExternalPortalEnabled;
        org.PortalBaseUrl = request.ExternalPortalEnabled ? request.PortalBaseUrl : null;
        if (!request.ExternalPortalEnabled)
        {
            org.PortalApiKey = null;
            org.PortalApiKeyHash = null;
        }
        else if (!string.IsNullOrEmpty(request.PortalApiKey))
        {
            // Only re-encrypt when a new key is supplied; otherwise keep the stored one.
            org.PortalApiKey = _credentialProtector.Protect(request.PortalApiKey);
            org.PortalApiKeyHash = ApiKeyHasher.Hash(request.PortalApiKey);
        }

        await _organizationRepository.UpdateAsync(org, cancellationToken);

        if (request.TradingPartnerIds is not null)
            await _organizationRepository.ReplacePartnersAsync(id, request.TradingPartnerIds.Distinct().ToList(), cancellationToken);

        _logger.LogInformation("Updated organization {OrgId}", id);
        return NoContent();
    }

    /// <summary>
    /// Approves a pending organization registration (Pending → Active). Also reactivates a
    /// suspended org. ("activate" is kept as an alias for backward compatibility.)
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> ApproveOrganization(int id, CancellationToken cancellationToken)
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
        org.RejectionReason = null;

        await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Approved organization {OrgId} ({OrgCode})", id, org.Code);
        return NoContent();
    }

    /// <summary>
    /// Rejects a pending organization registration (→ Rejected, with a reason).
    /// </summary>
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> RejectOrganization(int id, [FromBody] RejectRequest? request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound();

        org.Status = OrganizationStatus.Rejected;
        org.RejectionReason = request?.Reason;

        await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Rejected organization {OrgId} ({OrgCode})", id, org.Code);
        return NoContent();
    }

    /// <summary>
    /// Suspends an active organization.
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

    private static PaymentTerms ParsePaymentTerms(string? value) =>
        Enum.TryParse<PaymentTerms>(value, ignoreCase: true, out var pt) ? pt : PaymentTerms.CreditCard;

    private static OrganizationDto MapToDto(Organization org, int tenantCount)
    {
        return new OrganizationDto
        {
            Id = org.Id,
            Code = org.Code,
            Name = org.Name,
            Status = org.Status.ToString(),
            BillingPlanId = Guid.TryParse(org.BillingPlanId, out var planId) ? planId : null,
            PaymentTerms = org.PaymentTerms.ToString(),
            IsMultiTenant = org.IsMultiTenant,
            ExternalPortalEnabled = org.ExternalPortalEnabled,
            PortalBaseUrl = org.PortalBaseUrl,
            HasPortalApiKey = !string.IsNullOrEmpty(org.PortalApiKey),
            TenantCount = tenantCount,
            TradingPartnerIds = org.Partners?.Select(p => p.TradingPartnerId).ToList() ?? new List<int>(),
            CreatedAt = org.CreatedAt,
            ActivatedAt = org.ActivatedAt,
            SuspendedAt = org.SuspendedAt,
            RejectionReason = org.RejectionReason
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
    public string PaymentTerms { get; set; } = "CreditCard";
    public bool IsMultiTenant { get; set; }
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public bool HasPortalApiKey { get; set; }
    public int TenantCount { get; set; }
    public List<int> TradingPartnerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class OrganizationListResult
{
    public int Total { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public int PendingCount { get; set; }
    public List<OrganizationDto> Items { get; set; } = new();
}

public class BillingPlanOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long MonthlyPriceCents { get; set; }
    public string Currency { get; set; } = "USD";
}

public class CreateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? BillingPlanId { get; set; }
    public string? PaymentTerms { get; set; }
    public bool IsMultiTenant { get; set; }
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public string? PortalApiKey { get; set; }
    public List<int>? TradingPartnerIds { get; set; }
}

public class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? BillingPlanId { get; set; }
    public string? PaymentTerms { get; set; }
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public string? PortalApiKey { get; set; }
    public List<int>? TradingPartnerIds { get; set; }
}

public class SuspendRequest
{
    public string? Reason { get; set; }
}

public class RejectRequest
{
    public string? Reason { get; set; }
}
