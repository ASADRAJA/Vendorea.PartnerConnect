namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A public "request to join" an organization's customer portal, submitted by a prospective user who
/// knows the org (by its public code or name). It is a lightweight, anonymous intake: no user account
/// or activation token is created here. An OrgAdmin later reviews the request and, on approval, invites
/// the requester through the shared invite path (creating an Invited <see cref="OrgPortalUser"/> with a
/// chosen role + tenant scope and emailing an activation link). Kept separate from
/// <see cref="OrgPortalUser"/> so a pending/denied request never grants access.
/// </summary>
public class OrgAccessRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The organization the requester wants to join (resolved from the submitted code/name).</summary>
    public int OrganizationId { get; set; }

    /// <summary>The identifier the requester submitted (org code or name) — kept for audit/display.</summary>
    public string SubmittedOrganizationIdentifier { get; set; } = string.Empty;

    /// <summary>Requester's login email. The activation invite is sent here on approval.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Requester's display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional message from the requester (e.g. "I'm the new buyer for the Atlanta store").</summary>
    public string? Message { get; set; }

    /// <summary>Where the request currently sits in the review lifecycle.</summary>
    public OrgAccessRequestStatus Status { get; set; } = OrgAccessRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When an OrgAdmin approved/denied the request.</summary>
    public DateTime? DecisionAt { get; set; }

    /// <summary>The OrgAdmin (portal user id) who made the decision, when known.</summary>
    public Guid? DecisionByUserId { get; set; }

    /// <summary>Free-text reason recorded with a denial (or an approval note).</summary>
    public string? DecisionReason { get; set; }

    /// <summary>Navigation to the organization (optional; not always loaded).</summary>
    public Organization? Organization { get; set; }
}

/// <summary>Lifecycle of an <see cref="OrgAccessRequest"/>.</summary>
public enum OrgAccessRequestStatus
{
    /// <summary>Awaiting OrgAdmin review.</summary>
    Pending = 0,

    /// <summary>Approved: the requester was invited (Invited user + activation link).</summary>
    Approved = 1,

    /// <summary>Denied: the requester was notified; no account is created.</summary>
    Denied = 2
}
