using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// The public "request to join an organization" door. Anonymous by design: a prospective user names the
/// organization (by its public <see cref="Organization.Code"/>, or its name as a fallback) and submits
/// their email + display name. We resolve the org, record a pending <see cref="OrgAccessRequest"/> for
/// an OrgAdmin to review, and return 202. No user account or activation token is created here — that
/// happens only on OrgAdmin approval. Deliberately quiet: it never reveals whether an org exists.
/// </summary>
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
public class PublicAccessRequestsController : ControllerBase
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrgAccessRequestRepository _accessRequests;
    private readonly IOrgPortalUserRepository _users;
    private readonly ILogger<PublicAccessRequestsController> _logger;

    /// <summary>Same 202 message for every valid submission so an org's existence can't be probed.</summary>
    private const string AcceptedMessage =
        "Thanks — your request to join has been submitted. If the organization is found, an administrator " +
        "will review it and you'll get an email if you're approved.";

    public PublicAccessRequestsController(
        IOrganizationRepository organizations,
        IOrgAccessRequestRepository accessRequests,
        IOrgPortalUserRepository users,
        ILogger<PublicAccessRequestsController> logger)
    {
        _organizations = organizations;
        _accessRequests = accessRequests;
        _users = users;
        _logger = logger;
    }

    /// <summary>
    /// Submits a request to join an organization. Resolves the org by code (then name) among active
    /// orgs; on a match, records a pending request (deduped per email) unless the email is already a
    /// user of the org. Always returns the same 202 so callers can't enumerate orgs or members.
    /// </summary>
    [HttpPost("access-requests")]
    public async Task<IActionResult> RequestAccess([FromBody] PublicAccessRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request payload is required." });

        var orgIdentifier = request.OrganizationCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(orgIdentifier))
            return BadRequest(new { error = "An organization code or name is required." });

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName))
            return BadRequest(new { error = "Your name is required." });

        var email = request.Email?.Trim() ?? string.Empty;
        if (!IsValidEmail(email))
            return BadRequest(new { error = "A valid email is required." });

        var org = await ResolveActiveOrgAsync(orgIdentifier, cancellationToken);

        // Only act when the org resolves AND the email isn't already a member and has no pending
        // request. Every outcome returns the same 202 so the endpoint reveals nothing.
        if (org is not null
            && !await _users.ExistsAsync(org.Id, email, cancellationToken)
            && !await _accessRequests.HasPendingAsync(org.Id, email, cancellationToken))
        {
            var accessRequest = new OrgAccessRequest
            {
                OrganizationId = org.Id,
                SubmittedOrganizationIdentifier = orgIdentifier,
                Email = email,
                DisplayName = displayName,
                Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
                Status = OrgAccessRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await _accessRequests.AddAsync(accessRequest, cancellationToken);

            _logger.LogInformation("Received access request {RequestId} for org {OrgId} from {Email}",
                accessRequest.Id, org.Id, email);
        }
        else
        {
            _logger.LogInformation("Access request for identifier '{Identifier}' from {Email} was a no-op (unmatched/duplicate)",
                orgIdentifier, email);
        }

        return Accepted(new { message = AcceptedMessage });
    }

    /// <summary>Resolves an active org by code (case-insensitive), then by exact name. Null if none.</summary>
    private async Task<Organization?> ResolveActiveOrgAsync(string identifier, CancellationToken cancellationToken)
    {
        var byCode = await _organizations.GetByCodeAsync(identifier, cancellationToken);
        if (byCode is { Status: OrganizationStatus.Active })
            return byCode;

        var active = await _organizations.GetByStatusAsync(OrganizationStatus.Active, cancellationToken);
        return active.FirstOrDefault(o => string.Equals(o.Name, identifier, StringComparison.OrdinalIgnoreCase));
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

/// <summary>Body of <c>POST /api/v1/public/access-requests</c>.</summary>
public class PublicAccessRequest
{
    /// <summary>The organization's public code (or its name as a fallback).</summary>
    public string? OrganizationCode { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? Message { get; set; }
}
