using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Services;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Operator review queue for self-service organization registrations. Admin-key protected (via the
/// global fallback authorization policy, like the other admin controllers). Approving activates the
/// pending org, creates its plan subscription, and invites the first OrgAdmin (reusing the shared
/// Phase-1 invite path). Denying marks the org rejected and emails the applicant a courteous note.
/// </summary>
[ApiController]
[Route("api/v1/admin/org-registrations")]
public class AdminOrgRegistrationsController : ControllerBase
{
    private readonly IOrgRegistrationRequestRepository _registrations;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationOnboardingService _onboarding;
    private readonly IEmailSender _email;
    private readonly ILogger<AdminOrgRegistrationsController> _logger;

    public AdminOrgRegistrationsController(
        IOrgRegistrationRequestRepository registrations,
        IOrganizationRepository organizations,
        IOrganizationOnboardingService onboarding,
        IEmailSender email,
        ILogger<AdminOrgRegistrationsController> logger)
    {
        _registrations = registrations;
        _organizations = organizations;
        _onboarding = onboarding;
        _email = email;
        _logger = logger;
    }

    /// <summary>
    /// Lists registration requests, newest first. Defaults to the <c>Pending</c> queue; pass
    /// <c>?status=Approved|Denied|Pending</c> to filter, or <c>?status=All</c> for everything.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRegistrations([FromQuery] string? status, CancellationToken cancellationToken)
    {
        OrgRegistrationStatus? filter = OrgRegistrationStatus.Pending;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
                filter = null;
            else if (Enum.TryParse<OrgRegistrationStatus>(status, ignoreCase: true, out var parsed))
                filter = parsed;
            else
                return BadRequest(new { error = $"Invalid status '{status}'. Expected Pending, Approved, Denied, or All." });
        }

        var requests = await _registrations.GetAllAsync(filter, cancellationToken);
        return Ok(new OrgRegistrationListResult
        {
            Total = requests.Count,
            PendingCount = requests.Count(r => r.Status == OrgRegistrationStatus.Pending),
            Items = requests.Select(ToDto).ToList()
        });
    }

    /// <summary>
    /// Approves a pending registration: activates the org, creates the plan subscription, and invites
    /// the first OrgAdmin (Invited + activation email). Idempotency: only Pending requests are actionable.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var registration = await _registrations.GetByIdAsync(id, cancellationToken);
        if (registration is null)
            return NotFound(new { error = $"Registration {id} not found." });

        if (registration.Status != OrgRegistrationStatus.Pending)
            return BadRequest(new { error = $"Registration {id} has already been {registration.Status}." });

        var org = await _organizations.GetByIdAsync(registration.OrganizationId, cancellationToken);
        if (org is null)
            return NotFound(new { error = $"The organization for registration {id} no longer exists." });

        // Activate the org, create its subscription, and invite the first OrgAdmin (shared path).
        await _onboarding.OnboardOrganizationAsync(
            org, registration.BillingPlanId, registration.AdminEmail, registration.AdminDisplayName, cancellationToken);

        // Record the decision.
        registration.Status = OrgRegistrationStatus.Approved;
        registration.DecisionAt = DateTime.UtcNow;
        registration.DecisionByAdmin = User.Identity?.Name;
        await _registrations.UpdateAsync(registration, cancellationToken);

        _logger.LogInformation("Approved registration {RegistrationId} → org {OrgId} ({OrgCode}) activated",
            id, org.Id, org.Code);

        return Ok(new { message = $"Approved. {org.Name} is active and an activation email was sent to {registration.AdminEmail}." });
    }

    /// <summary>
    /// Denies a pending registration: marks the pending org Rejected (kept, not deleted, for audit),
    /// records the decision + reason, and emails the applicant a courteous note.
    /// </summary>
    [HttpPost("{id:int}/deny")]
    public async Task<IActionResult> Deny(int id, [FromBody] DenyRegistrationRequest? request, CancellationToken cancellationToken)
    {
        var registration = await _registrations.GetByIdAsync(id, cancellationToken);
        if (registration is null)
            return NotFound(new { error = $"Registration {id} not found." });

        if (registration.Status != OrgRegistrationStatus.Pending)
            return BadRequest(new { error = $"Registration {id} has already been {registration.Status}." });

        var reason = request?.Reason?.Trim();

        var org = await _organizations.GetByIdAsync(registration.OrganizationId, cancellationToken);
        if (org is not null)
        {
            // Keep the shell for audit rather than deleting — mark it Rejected so it never activates.
            org.Status = OrganizationStatus.Rejected;
            org.RejectionReason = reason;
            await _organizations.UpdateAsync(org, cancellationToken);
        }

        registration.Status = OrgRegistrationStatus.Denied;
        registration.DecisionAt = DateTime.UtcNow;
        registration.DecisionByAdmin = User.Identity?.Name;
        registration.DecisionReason = reason;
        await _registrations.UpdateAsync(registration, cancellationToken);

        await SendDenialEmailAsync(registration, reason, cancellationToken);

        _logger.LogInformation("Denied registration {RegistrationId} for '{OrgName}'", id, registration.OrganizationName);
        return Ok(new { message = $"Registration for {registration.OrganizationName} was denied and the applicant was notified." });
    }

    private async Task SendDenialEmailAsync(OrgRegistrationRequest registration, string? reason, CancellationToken cancellationToken)
    {
        var reasonHtml = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"<p>Reason: {System.Net.WebUtility.HtmlEncode(reason)}</p>";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"Reason: {reason}\n\n";

        var html =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(registration.AdminDisplayName)},</p>" +
            $"<p>Thank you for your interest in PartnerConnect. After reviewing your registration for " +
            $"<strong>{System.Net.WebUtility.HtmlEncode(registration.OrganizationName)}</strong>, we're unable to approve it at this time.</p>" +
            reasonHtml +
            "<p>If you believe this was a mistake or would like to discuss further, please reply to this email or contact our team.</p>";

        var text =
            $"Hello {registration.AdminDisplayName},\n\n" +
            $"Thank you for your interest in PartnerConnect. After reviewing your registration for " +
            $"{registration.OrganizationName}, we're unable to approve it at this time.\n\n" +
            reasonText +
            "If you believe this was a mistake or would like to discuss further, please reply to this email or contact our team.";

        await _email.SendAsync(registration.AdminEmail, "About your PartnerConnect registration", html, text, cancellationToken);
    }

    private static OrgRegistrationDto ToDto(OrgRegistrationRequest r) => new()
    {
        Id = r.Id,
        OrganizationId = r.OrganizationId,
        OrganizationName = r.OrganizationName,
        PlanCode = r.PlanCode,
        AdminDisplayName = r.AdminDisplayName,
        AdminEmail = r.AdminEmail,
        ContactPhone = r.ContactPhone,
        Status = r.Status.ToString(),
        SubmittedAt = r.SubmittedAt,
        DecisionAt = r.DecisionAt,
        DecisionByAdmin = r.DecisionByAdmin,
        DecisionReason = r.DecisionReason
    };
}

public class DenyRegistrationRequest
{
    public string? Reason { get; set; }
}

public class OrgRegistrationDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionByAdmin { get; set; }
    public string? DecisionReason { get; set; }
}

public class OrgRegistrationListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public List<OrgRegistrationDto> Items { get; set; } = new();
}
