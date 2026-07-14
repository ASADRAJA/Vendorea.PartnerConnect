using Vendorea.PartnerConnect.CustomerPortal.Models;

namespace Vendorea.PartnerConnect.CustomerPortal.Services;

/// <summary>
/// Scoped (per-circuit) portal state: the loaded org context and the currently-selected tenant.
/// Loaded once by the shell after login; pages read the selected tenant and subscribe to
/// <see cref="OnChange"/> so they can react to tenant switches.
/// </summary>
public class PortalState
{
    public OrgContextDto? Context { get; private set; }
    public OrgTenantDto? SelectedTenant { get; private set; }

    /// <summary>True once <see cref="SetContext"/> has run (even if the load returned null).</summary>
    public bool Loaded { get; private set; }

    public IReadOnlyList<OrgTenantDto> Tenants => Context?.Tenants ?? new List<OrgTenantDto>();

    /// <summary>The current user's role from <c>/org/me</c> (defaults to the most restrictive: Viewer).</summary>
    public string Role => Context?.User.Role ?? "Viewer";

    /// <summary>True when the current user is an OrgAdmin (drives the Organization nav + admin actions).</summary>
    public bool IsOrgAdmin => string.Equals(Role, "OrgAdmin", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the current user is a read-only Viewer (write actions are disabled).</summary>
    public bool IsViewer => string.Equals(Role, "Viewer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <c>/org/me</c> reported the given capability (e.g. <c>connections.write</c>,
    /// <c>settings.write</c>, <c>org.admin</c>). Pages gate write actions off this — it mirrors the
    /// server-side RBAC, so hiding/disabling here is a reflection, never the enforcement.
    /// </summary>
    public bool Can(string capability) =>
        Context?.Capabilities?.Contains(capability, StringComparer.OrdinalIgnoreCase) ?? false;

    /// <summary>Raised when the context loads or the selected tenant changes.</summary>
    public event Action? OnChange;

    public void SetContext(OrgContextDto? context)
    {
        Context = context;
        SelectedTenant = context?.Tenants.FirstOrDefault();
        Loaded = true;
        OnChange?.Invoke();
    }

    public void SelectTenant(OrgTenantDto? tenant)
    {
        SelectedTenant = tenant;
        OnChange?.Invoke();
    }
}
