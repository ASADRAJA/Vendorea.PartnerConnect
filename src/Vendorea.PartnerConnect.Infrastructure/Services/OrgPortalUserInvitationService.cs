using Microsoft.Extensions.Configuration;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// Shared invite path (see <see cref="IOrgPortalUserInvitationService"/>): create an Invited org
/// portal user with no password and email a single-use activation link. Reused by the admin bootstrap
/// controller and by self-service registration approval so both behave identically.
/// </summary>
public class OrgPortalUserInvitationService : IOrgPortalUserInvitationService
{
    /// <summary>How long an activation link stays valid.</summary>
    private static readonly TimeSpan ActivationLifetime = TimeSpan.FromDays(7);

    private const string DefaultPortalBaseUrl = "http://localhost:5030";

    private readonly IOrgPortalUserRepository _users;
    private readonly IOrgPortalUserTokenService _activationTokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;

    public OrgPortalUserInvitationService(
        IOrgPortalUserRepository users,
        IOrgPortalUserTokenService activationTokens,
        IEmailSender email,
        IConfiguration configuration)
    {
        _users = users;
        _activationTokens = activationTokens;
        _email = email;
        _configuration = configuration;
    }

    public async Task<OrgPortalUser> InviteAsync(
        Organization organization,
        string email,
        string? displayName,
        OrgPortalRole role,
        bool allTenants,
        IReadOnlyCollection<int>? tenantIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();

        var user = new OrgPortalUser
        {
            OrganizationId = organization.Id,
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
            Role = role,
            AllTenants = allTenants,
            IsActive = true,
            Status = OrgPortalUserStatus.Invited,
            // No password: the user sets their own via the activation link. Empty hash → login denied
            // until activated (PortalPasswordHasher.Verify returns false for an empty stored hash).
            PasswordHash = string.Empty
        };

        if (!allTenants && tenantIds is not null)
        {
            foreach (var tenantId in tenantIds)
                user.Tenants.Add(new OrgPortalUserTenant { OrgPortalUserId = user.Id, TenantId = tenantId });
        }

        await _users.AddAsync(user, cancellationToken);
        await SendActivationEmailAsync(user, organization, cancellationToken);
        return user;
    }

    public async Task SendActivationEmailAsync(OrgPortalUser user, Organization organization, CancellationToken cancellationToken = default)
    {
        var rawToken = await _activationTokens.IssueAsync(
            user.Id, OrgPortalUserTokenPurpose.Activation, ActivationLifetime, cancellationToken);

        var baseUrl = (_configuration["CustomerPortalBaseUrl"] ?? DefaultPortalBaseUrl).TrimEnd('/');
        var link = $"{baseUrl}/Account/Activate?token={Uri.EscapeDataString(rawToken)}";

        var html =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>" +
            $"<p>You've been invited to the <strong>{System.Net.WebUtility.HtmlEncode(organization.Name)}</strong> PartnerConnect portal. " +
            "Click the link below to set your password and activate your account:</p>" +
            $"<p><a href=\"{link}\">Activate your account</a></p>" +
            "<p>This link expires in 7 days. If you didn't expect this, you can ignore this email.</p>";

        var text =
            $"Hello {user.DisplayName},\n\n" +
            $"You've been invited to the {organization.Name} PartnerConnect portal. " +
            $"Set your password and activate your account here:\n{link}\n\n" +
            "This link expires in 7 days. If you didn't expect this, you can ignore this email.";

        await _email.SendAsync(user.Email, "Activate your PartnerConnect account", html, text, cancellationToken);
    }
}
