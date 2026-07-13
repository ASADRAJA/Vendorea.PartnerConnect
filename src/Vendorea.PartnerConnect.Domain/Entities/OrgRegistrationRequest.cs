namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A self-service organization registration submitted through the public customer-portal "Register"
/// door. Each request captures the applicant's intent (org name, chosen plan, the person who will
/// become the first OrgAdmin) plus the PC operator's decision. It is deliberately kept separate from
/// <see cref="Organization"/>: the org row is created immediately in a pending state (so a code is
/// reserved and duplicates are detectable), while this row carries the applicant + review/audit data
/// that does not belong on the long-lived organization record.
/// </summary>
public class OrgRegistrationRequest
{
    public int Id { get; set; }

    /// <summary>The pending <see cref="Organization"/> shell created for this registration.</summary>
    public int OrganizationId { get; set; }

    /// <summary>Requested organization display name (as typed by the applicant).</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>The billing plan the applicant selected.</summary>
    public Guid BillingPlanId { get; set; }

    /// <summary>The selected plan's code (denormalized for display in the review queue).</summary>
    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Name of the person who will become the org's first OrgAdmin.</summary>
    public string AdminDisplayName { get; set; } = string.Empty;

    /// <summary>Email of the intended first OrgAdmin. The activation invite is sent here on approval.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>Optional business contact phone.</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Where the request currently sits in the review lifecycle.</summary>
    public OrgRegistrationStatus Status { get; set; } = OrgRegistrationStatus.Pending;

    /// <summary>When the applicant submitted the registration.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When an operator approved/denied the request.</summary>
    public DateTime? DecisionAt { get; set; }

    /// <summary>Which operator made the decision (name from the admin ticket).</summary>
    public string? DecisionByAdmin { get; set; }

    /// <summary>Free-text reason recorded with a denial (or an approval note).</summary>
    public string? DecisionReason { get; set; }

    /// <summary>Navigation to the pending organization shell (optional; not always loaded).</summary>
    public Organization? Organization { get; set; }
}

/// <summary>Lifecycle of an <see cref="OrgRegistrationRequest"/>.</summary>
public enum OrgRegistrationStatus
{
    /// <summary>Awaiting operator review.</summary>
    Pending = 0,

    /// <summary>Approved: org activated, subscription created, OrgAdmin invited.</summary>
    Approved = 1,

    /// <summary>Denied: org marked rejected, applicant notified.</summary>
    Denied = 2
}
