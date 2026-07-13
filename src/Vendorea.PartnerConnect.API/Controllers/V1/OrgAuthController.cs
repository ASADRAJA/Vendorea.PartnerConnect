using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vendorea.PartnerConnect.Api.Authentication;
using Vendorea.PartnerConnect.Api.RateLimiting;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Infrastructure.Services;

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

    /// <summary>Minimum length for a self-chosen password at activation.</summary>
    private const int MinPasswordLength = 8;

    /// <summary>Generic message for any bad/expired/used activation token (no enumeration).</summary>
    private const string InvalidTokenError = "This activation link is invalid or has expired. Please request a new one.";

    /// <summary>Generic message for any bad/expired/used password-reset token (no enumeration).</summary>
    private const string InvalidResetTokenError = "This password reset link is invalid or has expired. Please request a new one.";

    /// <summary>Same reply for every forgot-password submission so accounts can't be enumerated.</summary>
    private const string ForgotPasswordMessage =
        "If an account exists for that email, a password reset link has been sent.";

    /// <summary>How long a password-reset link stays valid (short-lived on purpose).</summary>
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private const string DefaultPortalBaseUrl = "http://localhost:5030";

    private readonly IOrgPortalUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgUserTokenService _tokenService;
    private readonly IOrgPortalUserTokenService _activationTokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrgAuthController> _logger;

    public OrgAuthController(
        IOrgPortalUserRepository users,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgUserTokenService tokenService,
        IOrgPortalUserTokenService activationTokens,
        IEmailSender email,
        IConfiguration configuration,
        ILogger<OrgAuthController> logger)
    {
        _users = users;
        _organizations = organizations;
        _tenants = tenants;
        _tokenService = tokenService;
        _activationTokens = activationTokens;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Signs in an org portal user. On success returns a signed token, its expiry, and the user +
    /// organization summary. On any failure returns 401 with a generic message (never revealing which
    /// part failed). Repeated failures temporarily lock the account.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.PublicLogin)]
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

        // Only fully-Active users may sign in. Disabled users are blocked (deactivation also clears
        // IsActive), and an Invited user has no password so never reaches here anyway.
        if (!user.IsActive || user.Status != OrgPortalUserStatus.Active || !orgActive)
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

    /// <summary>
    /// Validates an activation link so the "set your password" page can show who it's for. Returns the
    /// invitee's email, display name, and organization name for a valid token; a generic 400 for any
    /// invalid / expired / already-used token (never reveals which).
    /// </summary>
    [HttpGet("activation")]
    public async Task<IActionResult> GetActivation([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var activation = await _activationTokens.ValidateAsync(
            token ?? string.Empty, OrgPortalUserTokenPurpose.Activation, cancellationToken);
        if (activation?.OrgPortalUser is null)
            return BadRequest(new { error = InvalidTokenError });

        var user = activation.OrgPortalUser;
        var organization = await _organizations.GetByIdAsync(user.OrganizationId, cancellationToken);

        return Ok(new ActivationInfoResponse
        {
            Email = user.Email,
            DisplayName = user.DisplayName,
            OrganizationName = organization?.Name ?? string.Empty
        });
    }

    /// <summary>
    /// Redeems an activation token: sets the user's chosen password, marks the account Active, clears
    /// any lockout state, and consumes the (single-use) token. Enforces a minimal password policy.
    /// The user then signs in normally via <c>POST /login</c>.
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = InvalidTokenError });

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
            return UnprocessableEntity(new { error = $"Password must be at least {MinPasswordLength} characters." });

        var activation = await _activationTokens.ValidateAsync(
            request.Token, OrgPortalUserTokenPurpose.Activation, cancellationToken);
        if (activation?.OrgPortalUser is null)
            return BadRequest(new { error = InvalidTokenError });

        var user = activation.OrgPortalUser;
        user.PasswordHash = PortalPasswordHasher.Hash(request.Password);
        user.Status = OrgPortalUserStatus.Active;
        user.IsActive = true;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _users.UpdateAsync(user, cancellationToken);

        // Single-use: consume the token so the link can't be replayed.
        await _activationTokens.ConsumeAsync(activation, cancellationToken);

        _logger.LogInformation("Org portal user {UserId} ({Email}) activated their account", user.Id, user.Email);

        return Ok(new { message = "Your password has been set. Please sign in." });
    }

    /// <summary>
    /// Starts a password reset. If an Active user with the given email exists, issues a short-lived
    /// single-use PasswordReset token and emails a reset link. ALWAYS returns 200 with the same generic
    /// message so callers can't tell whether the email is registered (no account enumeration). Rate
    /// limited to deter abuse.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.PublicAuth)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = request?.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Ok(new { message = ForgotPasswordMessage });

        var user = await _users.GetByEmailAsync(email, cancellationToken);

        // Only issue a reset for an activated, enabled account. Every other outcome still returns the
        // same 200 message below — the endpoint reveals nothing about the email's existence/state.
        if (user is { Status: OrgPortalUserStatus.Active, IsActive: true })
        {
            var organization = user.Organization
                ?? await _organizations.GetByIdAsync(user.OrganizationId, cancellationToken);
            await SendResetEmailAsync(user, organization, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Password reset requested for '{Email}' — no active account; no-op.", email);
        }

        return Ok(new { message = ForgotPasswordMessage });
    }

    /// <summary>
    /// Validates a password-reset link so the "set a new password" page can show whose account it is.
    /// Returns the account email for a valid token; a generic 400 for any invalid/expired/used token.
    /// </summary>
    [HttpGet("reset-password")]
    public async Task<IActionResult> GetResetPassword([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var reset = await _activationTokens.ValidateAsync(
            token ?? string.Empty, OrgPortalUserTokenPurpose.PasswordReset, cancellationToken);
        if (reset?.OrgPortalUser is null)
            return BadRequest(new { error = InvalidResetTokenError });

        return Ok(new ResetPasswordInfoResponse { Email = reset.OrgPortalUser.Email });
    }

    /// <summary>
    /// Redeems a password-reset token: sets the new password, clears any lockout state, activates the
    /// account if it was still Invited, and consumes the (single-use) token. Enforces the minimal
    /// password policy. The user then signs in normally via <c>POST /login</c>.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = InvalidResetTokenError });

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
            return UnprocessableEntity(new { error = $"Password must be at least {MinPasswordLength} characters." });

        var reset = await _activationTokens.ValidateAsync(
            request.Token, OrgPortalUserTokenPurpose.PasswordReset, cancellationToken);
        if (reset?.OrgPortalUser is null)
            return BadRequest(new { error = InvalidResetTokenError });

        var user = reset.OrgPortalUser;
        user.PasswordHash = PortalPasswordHasher.Hash(request.Password);
        // A reset also completes activation if the user never activated (e.g. lost invite link).
        if (user.Status == OrgPortalUserStatus.Invited)
            user.Status = OrgPortalUserStatus.Active;
        user.IsActive = true;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _users.UpdateAsync(user, cancellationToken);

        // Single-use: consume the token so the link can't be replayed.
        await _activationTokens.ConsumeAsync(reset, cancellationToken);

        _logger.LogInformation("Org portal user {UserId} ({Email}) reset their password", user.Id, user.Email);

        return Ok(new { message = "Your password has been reset. Please sign in." });
    }

    /// <summary>Issues a PasswordReset token and emails the branded reset link to the user.</summary>
    private async Task SendResetEmailAsync(OrgPortalUser user, Organization? organization, CancellationToken cancellationToken)
    {
        var rawToken = await _activationTokens.IssueAsync(
            user.Id, OrgPortalUserTokenPurpose.PasswordReset, ResetLifetime, cancellationToken);

        var baseUrl = (_configuration["CustomerPortalBaseUrl"] ?? DefaultPortalBaseUrl).TrimEnd('/');
        var link = $"{baseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(rawToken)}";

        var orgName = organization?.Name;
        var intro = string.IsNullOrWhiteSpace(orgName)
            ? "We received a request to reset the password for your PartnerConnect account. " +
              "Use the button below to choose a new password."
            : $"We received a request to reset the password for your {orgName} PartnerConnect account. " +
              "Use the button below to choose a new password.";

        var body = EmailTemplates.Build(
            user.DisplayName,
            new[] { intro },
            buttonText: "Reset your password",
            buttonUrl: link,
            footerNote: "This link expires in 1 hour. If you didn't request a reset, you can safely ignore this email.");

        await _email.SendAsync(user.Email, "Reset your PartnerConnect password", body.Html, body.Text, cancellationToken);
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

/// <summary>Body of <c>POST /api/v1/org/auth/activate</c>.</summary>
public class ActivateRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Response of <c>GET /api/v1/org/auth/activation</c> — context for the set-password page.</summary>
public class ActivationInfoResponse
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
}

/// <summary>Body of <c>POST /api/v1/org/auth/forgot-password</c>.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Response of <c>GET /api/v1/org/auth/reset-password</c> — context for the reset page.</summary>
public class ResetPasswordInfoResponse
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Body of <c>POST /api/v1/org/auth/reset-password</c>.</summary>
public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
