using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin bootstrap for customer-portal (org) login users. Called with the admin API key (enforced by
/// the global fallback authorization policy). An operator seeds an OrgAdmin (or any role); the user is
/// created <b>Invited with no password</b> and receives an activation link by email — no password is
/// ever set here or emailed. Org self-service user management is a later increment.
/// </summary>
[ApiController]
[Route("api/v1/admin/organizations/{orgId:int}/portal-users")]
public class AdminOrgPortalUsersController : ControllerBase
{
    /// <summary>How long an activation link stays valid.</summary>
    private static readonly TimeSpan ActivationLifetime = TimeSpan.FromDays(7);

    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgPortalUserTokenService _activationTokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminOrgPortalUsersController> _logger;

    public AdminOrgPortalUsersController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgPortalUserTokenService activationTokens,
        IEmailSender email,
        IConfiguration configuration,
        ILogger<AdminOrgPortalUsersController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _activationTokens = activationTokens;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Invites an org portal user: creates them Invited (no password) and emails an activation link.
    /// The user sets their own password via that link. Any password sent in the body is ignored.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(int orgId, [FromBody] CreateOrgPortalUserRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Expected OrgAdmin, TenantManager, or Viewer." });

        var org = await _organizations.GetByIdAsync(orgId, cancellationToken);
        if (org is null)
            return NotFound(new { error = $"Organization {orgId} not found." });

        var email = request.Email.Trim();
        if (await _users.ExistsAsync(orgId, email, cancellationToken))
            return Conflict(new { error = $"A user with email '{email}' already exists for this organization." });

        // Validate any scoped tenant ids belong to the org (only used when AllTenants is false).
        var tenantIds = (request.TenantIds ?? new List<int>()).Distinct().ToList();
        if (!request.AllTenants && tenantIds.Count > 0)
        {
            foreach (var tenantId in tenantIds)
            {
                var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
                if (tenant is null || tenant.OrganizationId != orgId)
                    return BadRequest(new { error = $"Tenant {tenantId} does not belong to organization {orgId}." });
            }
        }

        var user = new OrgPortalUser
        {
            OrganizationId = orgId,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName.Trim(),
            Role = role,
            AllTenants = request.AllTenants,
            IsActive = true,
            Status = OrgPortalUserStatus.Invited,
            // No password: the user sets their own via the activation link. Empty hash → login denied
            // until activated (PortalPasswordHasher.Verify returns false for an empty stored hash).
            PasswordHash = string.Empty
        };

        if (!request.AllTenants)
        {
            foreach (var tenantId in tenantIds)
                user.Tenants.Add(new OrgPortalUserTenant { OrgPortalUserId = user.Id, TenantId = tenantId });
        }

        await _users.AddAsync(user, cancellationToken);
        _logger.LogInformation("Invited org portal user {UserId} ({Email}) for org {OrgId} ({Role})",
            user.Id, user.Email, orgId, user.Role);

        await SendActivationEmailAsync(user, org, cancellationToken);

        return Ok(ToDto(user));
    }

    /// <summary>
    /// Regenerates an activation link for an Invited user and re-sends the invitation email. No-op-safe
    /// for users that already activated (returns 409 so the caller knows there's nothing to resend).
    /// </summary>
    [HttpPost("{userId:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(int orgId, Guid userId, CancellationToken cancellationToken)
    {
        var org = await _organizations.GetByIdAsync(orgId, cancellationToken);
        if (org is null)
            return NotFound(new { error = $"Organization {orgId} not found." });

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.OrganizationId != orgId)
            return NotFound(new { error = $"User {userId} not found for organization {orgId}." });

        if (user.Status != OrgPortalUserStatus.Invited)
            return Conflict(new { error = "This user has already activated their account." });

        await SendActivationEmailAsync(user, org, cancellationToken);
        _logger.LogInformation("Re-sent activation invite to org portal user {UserId} ({Email})", user.Id, user.Email);

        return Ok(new { message = $"Activation email re-sent to {user.Email}." });
    }

    /// <summary>Issues a fresh activation token and emails the set-your-password link.</summary>
    private async Task SendActivationEmailAsync(OrgPortalUser user, Organization org, CancellationToken cancellationToken)
    {
        var rawToken = await _activationTokens.IssueAsync(
            user.Id, OrgPortalUserTokenPurpose.Activation, ActivationLifetime, cancellationToken);

        var baseUrl = (_configuration["CustomerPortalBaseUrl"] ?? "http://localhost:5030").TrimEnd('/');
        var link = $"{baseUrl}/Account/Activate?token={Uri.EscapeDataString(rawToken)}";

        var html =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>" +
            $"<p>You've been invited to the <strong>{System.Net.WebUtility.HtmlEncode(org.Name)}</strong> PartnerConnect portal. " +
            "Click the link below to set your password and activate your account:</p>" +
            $"<p><a href=\"{link}\">Activate your account</a></p>" +
            "<p>This link expires in 7 days. If you didn't expect this, you can ignore this email.</p>";

        var text =
            $"Hello {user.DisplayName},\n\n" +
            $"You've been invited to the {org.Name} PartnerConnect portal. " +
            $"Set your password and activate your account here:\n{link}\n\n" +
            "This link expires in 7 days. If you didn't expect this, you can ignore this email.";

        await _email.SendAsync(user.Email, "Activate your PartnerConnect account", html, text, cancellationToken);
    }

    private static bool TryParseRole(string? value, out OrgPortalRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);

    private static OrgPortalUserDto ToDto(OrgPortalUser u) => new(
        u.Id,
        u.OrganizationId,
        u.Email,
        u.DisplayName,
        u.Role.ToString(),
        u.AllTenants,
        u.Tenants.Select(t => t.TenantId).ToList(),
        u.IsActive,
        u.Status.ToString(),
        u.CreatedAt);
}

public record CreateOrgPortalUserRequest(
    string Email,
    string? DisplayName,
    string Role,
    bool AllTenants,
    List<int>? TenantIds);

public record OrgPortalUserDto(
    Guid Id,
    int OrganizationId,
    string Email,
    string DisplayName,
    string Role,
    bool AllTenants,
    List<int> TenantIds,
    bool IsActive,
    string Status,
    DateTime CreatedAt);
