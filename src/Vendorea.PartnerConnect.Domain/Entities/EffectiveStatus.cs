namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Computes the EFFECTIVE active state across the organization → tenant → connection chain.
///
/// Invariant (by design): only an Active organization can have an effectively-active tenant,
/// and only an effectively-active tenant can have an effectively-active partner connection.
///
/// This is enforced as a runtime GUARD — we never mutate child rows when a parent is suspended.
/// A child keeps its own intrinsic status (so reactivating a parent does not blindly re-activate
/// children that were independently disabled); the chain is evaluated to decide whether the child
/// is actually usable right now.
/// </summary>
public static class EffectiveStatus
{
    /// <summary>An organization is active iff its status is <see cref="OrganizationStatus.Active"/>.</summary>
    public static bool IsOrganizationActive(Organization organization)
        => organization.Status == OrganizationStatus.Active;

    /// <summary>A tenant is effectively active iff it is Active AND its organization is Active.</summary>
    public static bool IsTenantEffectivelyActive(Tenant tenant, Organization organization)
        => tenant.Status == TenantStatus.Active && IsOrganizationActive(organization);

    /// <summary>
    /// A connection is effectively active iff it is approved and intrinsically active AND its
    /// tenant is effectively active (which in turn requires the organization to be active).
    /// </summary>
    public static bool IsConnectionEffectivelyActive(
        TenantPartnerAccount connection, Tenant tenant, Organization organization)
        => connection.IsActive
           && connection.ApprovalStatus == ConnectionApprovalStatus.Approved
           && IsTenantEffectivelyActive(tenant, organization);
}
