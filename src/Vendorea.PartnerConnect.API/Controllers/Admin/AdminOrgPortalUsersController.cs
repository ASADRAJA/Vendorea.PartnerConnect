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
    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgPortalUserInvitationService _invitations;
    private readonly ILogger<AdminOrgPortalUsersController> _logger;

    public AdminOrgPortalUsersController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgPortalUserInvitationService invitations,
        ILogger<AdminOrgPortalUsersController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _invitations = invitations;
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

        // Create the Invited user (no password) and email the activation link via the shared invite
        // path — the same path used by self-service registration approval.
        var user = await _invitations.InviteAsync(
            org, email, request.DisplayName, role, request.AllTenants, tenantIds, cancellationToken);

        _logger.LogInformation("Invited org portal user {UserId} ({Email}) for org {OrgId} ({Role})",
            user.Id, user.Email, orgId, user.Role);

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

        await _invitations.SendActivationEmailAsync(user, org, cancellationToken);
        _logger.LogInformation("Re-sent activation invite to org portal user {UserId} ({Email})", user.Id, user.Email);

        return Ok(new { message = $"Activation email re-sent to {user.Email}." });
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
