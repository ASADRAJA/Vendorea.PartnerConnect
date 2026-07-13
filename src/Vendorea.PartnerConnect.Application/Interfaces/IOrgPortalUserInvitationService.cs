using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Shared "invite an org portal user" path: create the user <b>Invited</b> (no password) and email a
/// single-use activation link (<c>{CustomerPortalBaseUrl}/Account/Activate?token=…</c>). Factored out
/// of the admin bootstrap controller so every invite path — operator-created users and
/// registration-approval OrgAdmins — creates the account and sends the email identically. No password
/// is ever set here or emailed; the user chooses their own via the link.
/// </summary>
public interface IOrgPortalUserInvitationService
{
    /// <summary>
    /// Creates an Invited <see cref="OrgPortalUser"/> for the organization (with the given role and
    /// tenant scope), persists it, then issues an activation token and emails the set-password link.
    /// Callers are responsible for their own validation (duplicate email, tenant ownership, etc.).
    /// </summary>
    Task<OrgPortalUser> InviteAsync(
        Organization organization,
        string email,
        string? displayName,
        OrgPortalRole role,
        bool allTenants,
        IReadOnlyCollection<int>? tenantIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a fresh activation token for an existing Invited user and re-sends the invitation email.
    /// Used by the "resend invite" path (no new user is created).
    /// </summary>
    Task SendActivationEmailAsync(
        OrgPortalUser user,
        Organization organization,
        CancellationToken cancellationToken = default);
}
