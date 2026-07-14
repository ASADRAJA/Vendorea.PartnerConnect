using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Services;

/// <summary>
/// Shared org-onboarding path (see <see cref="IOrganizationOnboardingService"/>): activate the org,
/// create its plan subscription (best-effort), and invite the first OrgAdmin.
/// </summary>
public class OrganizationOnboardingService : IOrganizationOnboardingService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrgPortalUserRepository _users;
    private readonly IOrgPortalUserInvitationService _invitations;
    private readonly IBillingService _billing;
    private readonly ILogger<OrganizationOnboardingService> _logger;

    public OrganizationOnboardingService(
        IOrganizationRepository organizations,
        IOrgPortalUserRepository users,
        IOrgPortalUserInvitationService invitations,
        IBillingService billing,
        ILogger<OrganizationOnboardingService> logger)
    {
        _organizations = organizations;
        _users = users;
        _invitations = invitations;
        _billing = billing;
        _logger = logger;
    }

    public async Task<OnboardResult> OnboardOrganizationAsync(
        Organization org,
        Guid planId,
        string adminEmail,
        string adminDisplayName,
        CancellationToken cancellationToken = default)
    {
        // 1) Activate the organization.
        org.Status = OrganizationStatus.Active;
        org.ActivatedAt = DateTime.UtcNow;
        org.SuspendedAt = null;
        org.SuspensionReason = null;
        org.RejectionReason = null;
        await _organizations.UpdateAsync(org, cancellationToken);

        // 2) Create the plan subscription (org is the billing subject). Don't fail onboarding if the
        // billing step hiccups (e.g. a subscription already exists) — activation + invite are the point.
        var subscriptionCreated = false;
        try
        {
            await _billing.CreateSubscriptionAsync(org.Id, planId, cancellationToken: cancellationToken);
            subscriptionCreated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Onboarded org {OrgId} but could not create a subscription for plan {PlanId}. Continuing.",
                org.Id, planId);
        }

        // 3) Invite the first OrgAdmin (Invited + activation email) unless one already exists.
        var inviteStatus = "Invited";
        if (!await _users.ExistsAsync(org.Id, adminEmail, cancellationToken))
        {
            await _invitations.InviteAsync(
                org, adminEmail, adminDisplayName,
                OrgPortalRole.OrgAdmin, allTenants: true, tenantIds: null, cancellationToken);
        }
        else
        {
            inviteStatus = "AlreadyExists";
            _logger.LogWarning("OrgAdmin {Email} already exists for org {OrgId}; skipped invite.",
                adminEmail, org.Id);
        }

        _logger.LogInformation("Onboarded org {OrgId} ({OrgCode}) — activated, admin {Email} {InviteStatus}",
            org.Id, org.Code, adminEmail, inviteStatus);

        return new OnboardResult
        {
            Organization = org,
            SubscriptionCreated = subscriptionCreated,
            AdminEmail = adminEmail,
            AdminInviteStatus = inviteStatus
        };
    }
}
