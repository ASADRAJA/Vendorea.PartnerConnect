namespace Vendorea.PartnerConnect.CustomerPortal.Models;

/// <summary>Response of <c>GET /api/v1/org/me</c> — the current org + user context.</summary>
public class OrgContextDto
{
    public OrgOrganizationDto Organization { get; set; } = new();
    public OrgUserDto User { get; set; } = new();
    public List<OrgTenantDto> Tenants { get; set; } = new();
    public List<string> Capabilities { get; set; } = new();
}

public class OrgOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class OrgUserDto
{
    public int? Id { get; set; }
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "OrgAdmin";
}

public class OrgTenantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Response row of <c>GET /api/v1/org/connections</c>.</summary>
public class OrgConnectionDto
{
    public int Id { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>Route-friendly identifier used to open the connection detail stub.</summary>
    public string Code => Id.ToString();

    /// <summary>Partner label for the grid; falls back to the trading-partner id.</summary>
    public string PartnerLabel => string.IsNullOrWhiteSpace(PartnerName) ? $"Partner {TradingPartnerId}" : PartnerName!;

    /// <summary>Display status: Active when live, otherwise the approval state (Pending/Denied/…).</summary>
    public string DisplayStatus => IsActive ? "Active" : ApprovalStatus;

    /// <summary>Best-known "last activity" timestamp for the grid.</summary>
    public DateTime? LastActivity => DecidedAt ?? CreatedAt;
}
