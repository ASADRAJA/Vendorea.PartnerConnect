using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authentication;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Security;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Native customer-portal authentication: an org user signs in with email + password and receives a
/// per-user JWT (see <see cref="OrgUserTokenService"/>). This is the human login path; the org API
/// key remains for machine/integration callers. Anonymous — the whole point is to obtain a token.
/// </summary>
[ApiController]
[Route("api/v1/org/auth")]
[AllowAnonymous]
public class OrgAuthController : ControllerBase
{
    /// <summary>Failed attempts before a temporary lockout kicks in.</summary>
    private const int MaxFailedAttempts = 5;

    /// <summary>Lockout cooldown once the failure threshold is reached.</summary>
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string GenericError = "Invalid email or password.";

    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgUserTokenService _tokenService;
    private readonly ILogger<OrgAuthController> _logger;

    public OrgAuthController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgUserTokenService tokenService,
        ILogger<OrgAuthController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Signs in an org portal user. On success returns a signed token, its expiry, and the user +
    /// organization summary. On any failure returns 401 with a generic message (never revealing which
    /// part failed). Repeated failures temporarily lock the account.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] OrgLoginRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = GenericError });

        var email = request.Email.Trim();

        // Optional org hint lets two orgs reuse an email; default treats email as globally unique.
        var user = request.OrganizationId.HasValue
            ? await _users.GetByOrgAndEmailAsync(request.OrganizationId.Value, email, cancellationToken)
            : await _users.GetByEmailAsync(email, cancellationToken);

        // Unknown user → generic failure (no enumeration).
        if (user is null)
            return Unauthorized(new { error = GenericError });

        // Locked out → generic failure. Doesn't reveal credential validity.
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Org portal login rejected for locked user {UserId}", user.Id);
            return Unauthorized(new { error = GenericError });
        }

        var passwordOk = PortalPasswordHasher.Verify(user.PasswordHash, request.Password);

        var organization = user.Organization
            ?? await _organizations.GetByIdAsync(user.OrganizationId, cancellationToken);
        var orgActive = organization is not null && organization.Status == OrganizationStatus.Active;

        // Any of: bad password, inactive user, inactive org → count as a failed attempt and return
        // the same generic error. A bad password is the only one that increments the lock counter.
        if (!passwordOk)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            await _users.UpdateAsync(user, cancellationToken);
            return Unauthorized(new { error = GenericError });
        }

        if (!user.IsActive || !orgActive)
            return Unauthorized(new { error = GenericError });

        // Success: clear the failure state and record the login.
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);

        var accessibleTenantIds = user.AllTenants
            ? null
            : user.Tenants.Select(t => t.TenantId).ToList();

        var minted = _tokenService.Issue(user, organization!, accessibleTenantIds);

        _logger.LogInformation("Org portal user {UserId} ({Email}) signed in to org {OrgId}",
            user.Id, user.Email, organization!.Id);

        return Ok(new OrgLoginResponse
        {
            Token = minted.Token,
            ExpiresAt = minted.ExpiresAtUtc,
            User = new OrgLoginUserDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Role = user.Role.ToString()
            },
            Organization = new OrgLoginOrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name
            }
        });
    }
}

/// <summary>Body of <c>POST /api/v1/org/auth/login</c>.</summary>
public class OrgLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional org hint when an email is not globally unique.</summary>
    public int? OrganizationId { get; set; }
}

public class OrgLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public OrgLoginUserDto User { get; set; } = new();
    public OrgLoginOrganizationDto Organization { get; set; } = new();
}

public class OrgLoginUserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class OrgLoginOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
