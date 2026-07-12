using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Security;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin bootstrap for customer-portal (org) login users. Called with the admin API key (enforced by
/// the global fallback authorization policy). Lets an operator seed the first OrgAdmin so the native
/// email/password login is testable; org self-service user management is a later increment.
/// </summary>
[ApiController]
[Route("api/v1/admin/organizations/{orgId:int}/portal-users")]
public class AdminOrgPortalUsersController : ControllerBase
{
    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly ILogger<AdminOrgPortalUsersController> _logger;

    public AdminOrgPortalUsersController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        ILogger<AdminOrgPortalUsersController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _logger = logger;
    }

    /// <summary>Creates an org portal user (hashes the password; never returns the hash).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(int orgId, [FromBody] CreateOrgPortalUserRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

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
            PasswordHash = PortalPasswordHasher.Hash(request.Password)
        };

        if (!request.AllTenants)
        {
            foreach (var tenantId in tenantIds)
                user.Tenants.Add(new OrgPortalUserTenant { OrgPortalUserId = user.Id, TenantId = tenantId });
        }

        await _users.AddAsync(user, cancellationToken);
        _logger.LogInformation("Created org portal user {UserId} ({Email}) for org {OrgId} ({Role})",
            user.Id, user.Email, orgId, user.Role);

        return Ok(ToDto(user));
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
        u.CreatedAt);
}

public record CreateOrgPortalUserRequest(
    string Email,
    string Password,
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
    DateTime CreatedAt);
