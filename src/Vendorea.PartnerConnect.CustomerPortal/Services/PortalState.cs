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
