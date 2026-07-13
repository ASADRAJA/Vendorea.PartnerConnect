using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// The public "Register your organization" door. Anonymous by design: a stranger submits an org name,
/// a chosen plan, and the intended first OrgAdmin's details. We create the organization in a pending
/// state and record an <see cref="OrgRegistrationRequest"/> for a PC operator to review. No usable
/// user or activation token is created here — that happens only on operator approval.
/// </summary>
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
public class PublicOrgRegistrationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrgRegistrationRequestRepository _registrations;
    private readonly IBillingPlanRepository _billingPlans;
    private readonly ILogger<PublicOrgRegistrationsController> _logger;

    public PublicOrgRegistrationsController(
        IOrganizationRepository organizations,
        IOrgRegistrationRequestRepository registrations,
        IBillingPlanRepository billingPlans,
        ILogger<PublicOrgRegistrationsController> logger)
    {
        _organizations = organizations;
        _registrations = registrations;
        _billingPlans = billingPlans;
        _logger = logger;
    }

    /// <summary>
    /// Lists the billing plans a self-service applicant can choose from (active plans only). Used to
    /// populate the public Register form's plan selector.
    /// </summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _billingPlans.GetAllAsync(includeInactive: false, cancellationToken);
        var items = plans
            .OrderBy(p => p.SortOrder)
            .Select(p => new PublicPlanDto
            {
                Code = p.Code,
                Name = p.Name,
                Blurb = p.Description,
                MonthlyPriceCents = p.MonthlyPriceCents,
                Currency = p.Currency
            })
            .ToList();
        return Ok(items);
    }

    /// <summary>
    /// Submits a self-service organization registration. Creates the org in <c>Pending</c> and records
    /// the request. Returns 202 Accepted with a "pending review" message. Deliberately lightweight
    /// (no user/token creation, no email) so the endpoint is cheap and abuse-resistant.
    /// </summary>
    [HttpPost("org-registrations")]
    public async Task<IActionResult> Register([FromBody] PublicOrgRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A registration payload is required." });

        var orgName = request.OrganizationName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(orgName))
            return BadRequest(new { error = "Organization name is required." });

        var adminName = request.AdminDisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(adminName))
            return BadRequest(new { error = "Administrator name is required." });

        var adminEmail = request.AdminEmail?.Trim() ?? string.Empty;
        if (!IsValidEmail(adminEmail))
            return BadRequest(new { error = "A valid administrator email is required." });

        if (string.IsNullOrWhiteSpace(request.PlanCode))
            return BadRequest(new { error = "A plan selection is required." });

        var plan = await _billingPlans.GetByCodeAsync(request.PlanCode.Trim(), cancellationToken);
        if (plan is null || !plan.IsActive)
            return BadRequest(new { error = $"Plan '{request.PlanCode}' is not available." });

        // Reject a duplicate of an already-active organization (case-insensitive).
        var activeOrgs = await _organizations.GetByStatusAsync(OrganizationStatus.Active, cancellationToken);
        if (activeOrgs.Any(o => string.Equals(o.Name, orgName, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = "An organization with this name already exists. Please contact support." });

        // Create the pending organization shell (reserves a code + makes duplicates detectable).
        var organization = new Organization
        {
            Code = await _organizations.GenerateNextCodeAsync(cancellationToken),
            Name = orgName,
            ContactEmail = adminEmail,
            ContactPhone = request.ContactPhone?.Trim(),
            BillingPlanId = plan.Id.ToString(),
            Status = OrganizationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _organizations.AddAsync(organization, cancellationToken);

        var registration = new OrgRegistrationRequest
        {
            OrganizationId = organization.Id,
            OrganizationName = orgName,
            BillingPlanId = plan.Id,
            PlanCode = plan.Code,
            AdminDisplayName = adminName,
            AdminEmail = adminEmail,
            ContactPhone = request.ContactPhone?.Trim(),
            Status = OrgRegistrationStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        await _registrations.AddAsync(registration, cancellationToken);

        _logger.LogInformation(
            "Received org registration {RegistrationId} for '{OrgName}' (org {OrgId}, plan {PlanCode})",
            registration.Id, orgName, organization.Id, plan.Code);

        return Accepted(new
        {
            message = "Thanks — your registration has been received and is under review. " +
                      "You'll get an email when it's approved."
        });
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Body of <c>POST /api/v1/public/org-registrations</c>.</summary>
public class PublicOrgRegistrationRequest
{
    public string? OrganizationName { get; set; }
    public string? PlanCode { get; set; }
    public string? AdminDisplayName { get; set; }
    public string? AdminEmail { get; set; }
    public string? ContactPhone { get; set; }
}

/// <summary>A selectable plan returned by <c>GET /api/v1/public/plans</c>.</summary>
public class PublicPlanDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Blurb { get; set; }
    public long MonthlyPriceCents { get; set; }
    public string Currency { get; set; } = "USD";
}
