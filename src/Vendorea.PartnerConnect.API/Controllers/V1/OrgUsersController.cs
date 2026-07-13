using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Services;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// OrgAdmin self-service user management for the customer portal. All endpoints are org-admin only
/// (<see cref="ApiScopes.OrgAdmin"/> — granted to an OrgAdmin portal-user token or the org API key) and
/// are association-gated to the caller's own organization (resolved from the <c>org_id</c> claim).
/// Covers invite / edit (role + tenant scope + status) / resend / deactivate-reactivate, plus the
/// OrgAdmin side of the public "request to join" flow (list / approve / deny).
/// </summary>
[ApiController]
[Route("api/v1/org")]
[RequireScope(ApiScopes.OrgAdmin)]
public class OrgUsersController : ControllerBase
{
    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgPortalUserInvitationService _invitations;
    private readonly IOrgAccessRequestRepository _accessRequests;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;
    private readonly ILogger<OrgUsersController> _logger;

    public OrgUsersController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgPortalUserInvitationService invitations,
        IOrgAccessRequestRepository accessRequests,
        IEmailSender email,
        IAuditService audit,
        ILogger<OrgUsersController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _invitations = invitations;
        _accessRequests = accessRequests;
        _email = email;
        _audit = audit;
        _logger = logger;
    }

    // ============================================================================================
    // Users
    // ============================================================================================

    /// <summary>Lists the organization's portal users with role, status, and tenant scope.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var users = await _users.GetByOrganizationIdAsync(org.Id, cancellationToken);
        var tenantNames = await GetTenantNameMapAsync(org.Id, cancellationToken);

        return Ok(users.Select(u => ToDto(u, tenantNames)).ToList());
    }

    /// <summary>
    /// Invites a new portal user: validates the email is unique within the org and (when scoped) that
    /// each tenant belongs to the org, then creates the user Invited and emails an activation link.
    /// </summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] OrgUserWriteRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Expected OrgAdmin, TenantManager, or Viewer." });

        var email = request.Email.Trim();
        if (await _users.ExistsAsync(org.Id, email, cancellationToken))
            return Conflict(new { error = $"A user with email '{email}' already exists for this organization." });

        var (tenantIds, tenantError) = await ValidateScopeAsync(org.Id, request.AllTenants, request.TenantIds, cancellationToken);
        if (tenantError is not null)
            return tenantError;

        var user = await _invitations.InviteAsync(
            org, email, request.DisplayName, role, request.AllTenants, tenantIds, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Create, "OrgPortalUser", user.Id.ToString(),
            newValues: new { user.Email, Role = user.Role.ToString(), user.AllTenants, TenantIds = tenantIds },
            notes: $"User invited to org {org.Id}.", cancellationToken: cancellationToken);

        _logger.LogInformation("OrgAdmin invited portal user {UserId} ({Email}) to org {OrgId} as {Role}",
            user.Id, user.Email, org.Id, user.Role);

        var tenantNames = await GetTenantNameMapAsync(org.Id, cancellationToken);
        return Ok(ToDto(user, tenantNames));
    }

    /// <summary>
    /// Updates a user's role, tenant scope, and status (Active/Disabled). Guards against removing the
    /// org's last active OrgAdmin (whether by demotion or by disabling), which also blocks an admin
    /// from demoting/disabling themselves when they're the last one.
    /// </summary>
    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] OrgUserUpdateRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Expected OrgAdmin, TenantManager, or Viewer." });

        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || user.OrganizationId != org.Id)
            return NotFound(new { error = $"User '{id}' not found." });

        // Resolve the target status. Omitting it keeps the current one; an Invited user stays Invited
        // (activation is the only path to Active) unless explicitly disabled.
        var newStatus = user.Status;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<OrgPortalUserStatus>(request.Status, ignoreCase: true, out var parsedStatus)
                || parsedStatus == OrgPortalUserStatus.Invited)
                return BadRequest(new { error = $"Invalid status '{request.Status}'. Expected Active or Disabled." });
            newStatus = parsedStatus;
        }
        var newIsActive = newStatus != OrgPortalUserStatus.Disabled;

        var (tenantIds, tenantError) = await ValidateScopeAsync(org.Id, request.AllTenants, request.TenantIds, cancellationToken);
        if (tenantError is not null)
            return tenantError;

        if (await WouldRemoveLastActiveAdminAsync(org.Id, user, role, newStatus, newIsActive, cancellationToken))
            return Conflict(new { error = "Cannot remove the last active Org Admin. Assign another Org Admin first." });

        var previous = new { Role = user.Role.ToString(), Status = user.Status.ToString(), user.AllTenants };

        user.Role = role;
        user.Status = newStatus;
        user.IsActive = newIsActive;
        user.AllTenants = request.AllTenants;

        await _users.UpdateWithTenantScopeAsync(user, request.AllTenants, tenantIds, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Update, "OrgPortalUser", user.Id.ToString(),
            oldValues: previous,
            newValues: new { Role = user.Role.ToString(), Status = user.Status.ToString(), user.AllTenants, TenantIds = tenantIds },
            notes: $"User role/status/scope changed in org {org.Id}.", cancellationToken: cancellationToken);

        _logger.LogInformation("OrgAdmin updated portal user {UserId} in org {OrgId} (role {Role}, status {Status})",
            user.Id, org.Id, user.Role, user.Status);

        var tenantNames = await GetTenantNameMapAsync(org.Id, cancellationToken);
        return Ok(ToDto(user, tenantNames));
    }

    /// <summary>Regenerates the activation link for an Invited user and re-sends the invitation email.</summary>
    [HttpPost("users/{id:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || user.OrganizationId != org.Id)
            return NotFound(new { error = $"User '{id}' not found." });

        if (user.Status != OrgPortalUserStatus.Invited)
            return Conflict(new { error = "This user has already activated their account." });

        await _invitations.SendActivationEmailAsync(user, org, cancellationToken);
        _logger.LogInformation("OrgAdmin re-sent activation invite to portal user {UserId} ({Email})", user.Id, user.Email);

        return Ok(new { message = $"Activation email re-sent to {user.Email}." });
    }

    /// <summary>Deactivates a user (Disabled): they can no longer sign in. Guards the last active admin.</summary>
    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, active: false, cancellationToken);

    /// <summary>Reactivates a previously-disabled user (back to Active).</summary>
    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, active: true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || user.OrganizationId != org.Id)
            return NotFound(new { error = $"User '{id}' not found." });

        // Reactivating an Invited (never-activated) user makes no sense — there's no password yet.
        if (active && user.Status == OrgPortalUserStatus.Invited)
            return Conflict(new { error = "This user hasn't activated their account yet. Resend the invite instead." });

        var newStatus = active ? OrgPortalUserStatus.Active : OrgPortalUserStatus.Disabled;

        if (!active && await WouldRemoveLastActiveAdminAsync(org.Id, user, user.Role, newStatus, isActive: false, cancellationToken))
            return Conflict(new { error = "Cannot deactivate the last active Org Admin. Assign another Org Admin first." });

        user.Status = newStatus;
        user.IsActive = active;
        await _users.UpdateAsync(user, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Update, "OrgPortalUser", user.Id.ToString(),
            newValues: new { Status = newStatus.ToString(), user.IsActive },
            notes: $"User {(active ? "reactivated" : "deactivated")} in org {org.Id}.",
            cancellationToken: cancellationToken);

        _logger.LogInformation("OrgAdmin {Action} portal user {UserId} in org {OrgId}",
            active ? "reactivated" : "deactivated", user.Id, org.Id);

        var tenantNames = await GetTenantNameMapAsync(org.Id, cancellationToken);
        return Ok(ToDto(user, tenantNames));
    }

    // ============================================================================================
    // Access requests (OrgAdmin side of the public request-to-join flow)
    // ============================================================================================

    /// <summary>Lists the org's join requests, optionally filtered by status (default: all).</summary>
    [HttpGet("access-requests")]
    public async Task<IActionResult> GetAccessRequests([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        OrgAccessRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrgAccessRequestStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(new { error = $"Invalid status '{status}'. Expected Pending, Approved, or Denied." });
            statusFilter = parsed;
        }

        var requests = await _accessRequests.GetByOrganizationIdAsync(org.Id, statusFilter, cancellationToken);
        return Ok(requests.Select(ToAccessRequestDto).ToList());
    }

    /// <summary>
    /// Approves a pending join request: invites the requester with the chosen role + tenant scope
    /// (Invited user + activation link) and marks the request Approved. Rejects if the requester is
    /// already a user of the org.
    /// </summary>
    [HttpPost("access-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAccessRequest(Guid id, [FromBody] OrgUserWriteRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        if (request is null || !TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request?.Role}'. Expected OrgAdmin, TenantManager, or Viewer." });

        var accessRequest = await _accessRequests.GetByIdAsync(id, cancellationToken);
        if (accessRequest is null || accessRequest.OrganizationId != org.Id)
            return NotFound(new { error = $"Access request '{id}' not found." });

        if (accessRequest.Status != OrgAccessRequestStatus.Pending)
            return Conflict(new { error = "This request has already been decided." });

        if (await _users.ExistsAsync(org.Id, accessRequest.Email, cancellationToken))
            return Conflict(new { error = $"A user with email '{accessRequest.Email}' already exists for this organization." });

        var (tenantIds, tenantError) = await ValidateScopeAsync(org.Id, request.AllTenants, request.TenantIds, cancellationToken);
        if (tenantError is not null)
            return tenantError;

        var user = await _invitations.InviteAsync(
            org, accessRequest.Email, accessRequest.DisplayName, role, request.AllTenants, tenantIds, cancellationToken);

        accessRequest.Status = OrgAccessRequestStatus.Approved;
        accessRequest.DecisionAt = DateTime.UtcNow;
        accessRequest.DecisionByUserId = CurrentUserId();
        await _accessRequests.UpdateAsync(accessRequest, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Update, "OrgAccessRequest", accessRequest.Id.ToString(),
            newValues: new { Status = "Approved", accessRequest.Email, Role = role.ToString(), InvitedUserId = user.Id },
            notes: $"Access request approved; user invited to org {org.Id}.", cancellationToken: cancellationToken);

        _logger.LogInformation("OrgAdmin approved access request {RequestId} → invited user {UserId} ({Email}) to org {OrgId}",
            accessRequest.Id, user.Id, user.Email, org.Id);

        var tenantNames = await GetTenantNameMapAsync(org.Id, cancellationToken);
        return Ok(ToDto(user, tenantNames));
    }

    /// <summary>Denies a pending join request (records the reason and emails the requester).</summary>
    [HttpPost("access-requests/{id:guid}/deny")]
    public async Task<IActionResult> DenyAccessRequest(Guid id, [FromBody] DenyAccessRequestRequest? request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var accessRequest = await _accessRequests.GetByIdAsync(id, cancellationToken);
        if (accessRequest is null || accessRequest.OrganizationId != org.Id)
            return NotFound(new { error = $"Access request '{id}' not found." });

        if (accessRequest.Status != OrgAccessRequestStatus.Pending)
            return Conflict(new { error = "This request has already been decided." });

        accessRequest.Status = OrgAccessRequestStatus.Denied;
        accessRequest.DecisionAt = DateTime.UtcNow;
        accessRequest.DecisionByUserId = CurrentUserId();
        accessRequest.DecisionReason = request?.Reason?.Trim();
        await _accessRequests.UpdateAsync(accessRequest, cancellationToken);

        await SendDenyEmailAsync(accessRequest, org, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Update, "OrgAccessRequest", accessRequest.Id.ToString(),
            newValues: new { Status = "Denied", accessRequest.Email, accessRequest.DecisionReason },
            notes: $"Access request denied for org {org.Id}.", cancellationToken: cancellationToken);

        _logger.LogInformation("OrgAdmin denied access request {RequestId} for org {OrgId}", accessRequest.Id, org.Id);

        return Ok(ToAccessRequestDto(accessRequest));
    }

    // ============================================================================================
    // Helpers
    // ============================================================================================

    /// <summary>
    /// Resolves the caller's organization from the validated <c>org_id</c> claim (present on both an
    /// OrgAdmin user token and the org API key) and confirms it's active. 401 otherwise.
    /// </summary>
    private async Task<(Organization? Org, IActionResult? Error)> ResolveOrgAsync(CancellationToken cancellationToken)
    {
        var orgIdClaim = User.FindFirst(ApiPrincipalExtensions.OrgIdClaim)?.Value;
        if (!int.TryParse(orgIdClaim, out var orgId))
            return (null, Unauthorized(new { error = "No organization context." }));

        var org = await _organizations.GetByIdAsync(orgId, cancellationToken);
        if (org is null || org.Status != OrganizationStatus.Active)
            return (null, Unauthorized(new { error = "Invalid or inactive organization." }));

        return (org, null);
    }

    /// <summary>The calling portal user's id (from the token's <c>sub</c>), or null for the org-key path.</summary>
    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var g) ? g : null;

    /// <summary>
    /// Validates the requested tenant scope: when not all-tenants, each tenant id must belong to the
    /// org (404-style BadRequest otherwise). Returns the de-duplicated ids to persist (null when
    /// all-tenants).
    /// </summary>
    private async Task<(IReadOnlyCollection<int>? TenantIds, IActionResult? Error)> ValidateScopeAsync(
        int organizationId, bool allTenants, List<int>? tenantIds, CancellationToken cancellationToken)
    {
        if (allTenants)
            return (null, null);

        var ids = (tenantIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return (null, BadRequest(new { error = "Select at least one tenant, or grant access to all tenants." }));

        foreach (var tenantId in ids)
        {
            var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null || tenant.OrganizationId != organizationId)
                return (null, NotFound(new { error = $"Tenant '{tenantId}' not found for this organization." }));
        }

        return (ids, null);
    }

    /// <summary>
    /// True when applying the proposed role/status to <paramref name="target"/> would leave the org
    /// with zero active OrgAdmins (Role=OrgAdmin, Status=Active, IsActive). Only blocks when the target
    /// currently counts as an active admin (so we never fabricate a lockout that didn't exist).
    /// </summary>
    private async Task<bool> WouldRemoveLastActiveAdminAsync(
        int organizationId, OrgPortalUser target, OrgPortalRole newRole, OrgPortalUserStatus newStatus, bool isActive,
        CancellationToken cancellationToken)
    {
        var isActiveAdmin = target.Role == OrgPortalRole.OrgAdmin
            && target.Status == OrgPortalUserStatus.Active
            && target.IsActive;
        var willBeActiveAdmin = newRole == OrgPortalRole.OrgAdmin
            && newStatus == OrgPortalUserStatus.Active
            && isActive;

        if (!isActiveAdmin || willBeActiveAdmin)
            return false;

        var all = await _users.GetByOrganizationIdAsync(organizationId, cancellationToken);
        var otherActiveAdmins = all.Count(u =>
            u.Id != target.Id
            && u.Role == OrgPortalRole.OrgAdmin
            && u.Status == OrgPortalUserStatus.Active
            && u.IsActive);

        return otherActiveAdmins == 0;
    }

    private async Task<Dictionary<int, string>> GetTenantNameMapAsync(int organizationId, CancellationToken cancellationToken)
    {
        var tenants = await _tenants.GetByOrganizationIdAsync(organizationId, cancellationToken);
        return tenants.ToDictionary(t => t.Id, t => t.Name);
    }

    private async Task SendDenyEmailAsync(OrgAccessRequest request, Organization org, CancellationToken cancellationToken)
    {
        var paragraphs = new List<string>
        {
            $"Your request to join the {org.Name} PartnerConnect portal was not approved at this time."
        };
        if (!string.IsNullOrWhiteSpace(request.DecisionReason))
            paragraphs.Add($"Reason: {request.DecisionReason}");

        var body = EmailTemplates.Build(
            request.DisplayName,
            paragraphs,
            footerNote: "If you believe this was a mistake, please contact your organization's administrator.");

        await _email.SendAsync(request.Email, "Your PartnerConnect access request", body.Html, body.Text, cancellationToken);
    }

    private static bool TryParseRole(string? value, out OrgPortalRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);

    private static OrgUserResponse ToDto(OrgPortalUser u, IReadOnlyDictionary<int, string> tenantNames)
    {
        var ids = u.Tenants.Select(t => t.TenantId).ToList();
        return new OrgUserResponse(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role.ToString(),
            u.Status.ToString(),
            u.AllTenants,
            ids,
            ids.Select(id => tenantNames.TryGetValue(id, out var name) ? name : $"Tenant {id}").ToList(),
            u.LastLoginAt,
            u.CreatedAt);
    }

    private static OrgAccessRequestResponse ToAccessRequestDto(OrgAccessRequest r) => new(
        r.Id,
        r.Email,
        r.DisplayName,
        r.Message,
        r.Status.ToString(),
        r.CreatedAt,
        r.DecisionAt,
        r.DecisionReason);
}

/// <summary>Body of <c>POST /api/v1/org/users</c> and <c>POST /api/v1/org/access-requests/{id}/approve</c>.</summary>
public class OrgUserWriteRequest
{
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string Role { get; set; } = nameof(OrgPortalRole.Viewer);
    public bool AllTenants { get; set; } = true;
    public List<int>? TenantIds { get; set; }
}

/// <summary>Body of <c>PUT /api/v1/org/users/{id}</c> — role + scope + (optional) status.</summary>
public class OrgUserUpdateRequest
{
    public string Role { get; set; } = nameof(OrgPortalRole.Viewer);
    public bool AllTenants { get; set; } = true;
    public List<int>? TenantIds { get; set; }

    /// <summary>Optional status change: Active or Disabled. Omit to leave the current status unchanged.</summary>
    public string? Status { get; set; }
}

/// <summary>Body of <c>POST /api/v1/org/access-requests/{id}/deny</c>.</summary>
public class DenyAccessRequestRequest
{
    public string? Reason { get; set; }
}

/// <summary>A row in the org's Users list.</summary>
public record OrgUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    bool AllTenants,
    List<int> TenantIds,
    List<string> TenantNames,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

/// <summary>A join request in the OrgAdmin queue.</summary>
public record OrgAccessRequestResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? Message,
    string Status,
    DateTime CreatedAt,
    DateTime? DecisionAt,
    string? DecisionReason);
