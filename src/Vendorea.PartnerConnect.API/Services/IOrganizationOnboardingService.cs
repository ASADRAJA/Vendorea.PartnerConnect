using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Services;

/// <summary>
/// Shared "activate an organization and stand it up" path. Factored out of the registration-approval
/// controller so both the self-service approval flow and the operator-led onboarding endpoint activate
/// the org, create its plan subscription, and invite the first OrgAdmin identically. Lives in the API
/// project because it depends on <c>IBillingService</c> (referenced only by API/Persistence).
/// </summary>
public interface IOrganizationOnboardingService
{
    /// <summary>
    /// Activates <paramref name="org"/> (Active + saved), creates its plan subscription (best-effort —
    /// logged and skipped on failure), then invites the first OrgAdmin (AllTenants scope) unless one
    /// already exists for the org. Returns a summary of what happened for the caller to surface.
    /// </summary>
    Task<OnboardResult> OnboardOrganizationAsync(
        Organization org,
        Guid planId,
        string adminEmail,
        string adminDisplayName,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IOrganizationOnboardingService.OnboardOrganizationAsync"/>.</summary>
public class OnboardResult
{
    /// <summary>The (now Active) organization.</summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>True if a plan subscription was created; false if the billing step failed (and was skipped).</summary>
    public bool SubscriptionCreated { get; set; }

    /// <summary>The email the OrgAdmin invite was (or would have been) sent to.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>"Invited" when a fresh activation invite was emailed; "AlreadyExists" when an OrgAdmin already existed.</summary>
    public string AdminInviteStatus { get; set; } = "Invited";
}
