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

/// <summary>Detail for a single connection — <c>GET /api/v1/org/connections/{id}</c>.</summary>
public class OrgConnectionDetailDto
{
    public int Id { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public int TradingPartnerId { get; set; }
    public int? TenantId { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public List<string> Capabilities { get; set; } = new();
    public OrgConnectionConfigDto Config { get; set; } = new();
    public OrgConnectionHealthDto Health { get; set; } = new();
}

public class OrgConnectionConfigDto
{
    public string PartnerAccountNumber { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public List<string> SelectedDistributionCenters { get; set; } = new();
    public OrgConnectionPreferencesDto Preferences { get; set; } = new();
    public OrgConnectionTransportDto Transport { get; set; } = new();
}

public class OrgConnectionPreferencesDto
{
    public string? OrderType { get; set; }
    public bool? ContentSubscribed { get; set; }
}

public class OrgConnectionTransportDto
{
    public string? Type { get; set; }
    public string? Host { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
    public bool Editable { get; set; }
    public bool ManagedByOperator { get; set; } = true;
}

public class OrgConnectionHealthDto
{
    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Body of <c>PUT /api/v1/org/connections/{id}</c>.</summary>
public class UpdateOrgConnectionRequest
{
    public string? PartnerAccountNumber { get; set; }
    public string? DisplayName { get; set; }
    public List<string>? SelectedDistributionCenters { get; set; }
    public OrgConnectionPreferencesDto? Preferences { get; set; }
    public string? TransportPassword { get; set; }
}

/// <summary>A partner distribution center — <c>GET /api/v1/org/partners/{code}/distribution-centers</c>.</summary>
public class OrgDistributionCenterDto
{
    public int DcNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    /// <summary>The value stored in a connection's SelectedDistributionCenters (the DC number as text).</summary>
    public string Code => DcNumber.ToString();

    /// <summary>Grid/checkbox label, e.g. "9 — Atlanta, GA".</summary>
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Label) ? Code : $"{DcNumber} — {Label}";
}

/// <summary>Outcome of a connection mutation (update/suspend/disconnect).</summary>
public record ConnectionActionResult(bool Success, string? Error, OrgConnectionDetailDto? Connection)
{
    public static ConnectionActionResult Ok(OrgConnectionDetailDto? c) => new(true, null, c);
    public static ConnectionActionResult Fail(string error) => new(false, error, null);
}

// ============================================================================================
// Catalog (increment 3) — prices, inventory, content. Mirror the org API DTOs.
// ============================================================================================

/// <summary>Generic paged list envelope: <c>{ items, total, skip, take }</c>.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }

    /// <summary>True when the rows are partner-level shared data (not per-tenant), e.g. inventory.</summary>
    public bool PartnerLevel { get; set; }
}

/// <summary>A partner the selected tenant is connected to — drives the catalog partner selector.</summary>
public class OrgCatalogPartnerDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>A current-price row (<c>GET /org/tenants/{id}/prices</c>).</summary>
public class PriceRowDto
{
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Cost { get; set; }
    public decimal ListPrice { get; set; }
    public string? Uom { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>Price history for a SKU (<c>GET /org/tenants/{id}/prices/{sku}/history</c>).</summary>
public class PriceHistoryDto
{
    public string Sku { get; set; } = string.Empty;
    public List<PriceHistoryPointDto> Points { get; set; } = new();
}

public class PriceHistoryPointDto
{
    public decimal Cost { get; set; }
    public decimal ListPrice { get; set; }
    public string? Uom { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Stock-by-DC for a SKU (<c>GET /org/tenants/{id}/inventory</c>). Partner-level data.</summary>
public class InventoryRowDto
{
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Uom { get; set; }
    public DateTime? AsOf { get; set; }
    public List<InventoryDcDto> ByDistributionCenter { get; set; } = new();

    /// <summary>Convenience total across DCs for the grid.</summary>
    public int TotalOnHand => ByDistributionCenter.Sum(d => d.OnHand);
}

public class InventoryDcDto
{
    public string Dc { get; set; } = string.Empty;
    public string? DcName { get; set; }
    public int OnHand { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Content coverage summary (<c>GET /org/tenants/{id}/content/summary</c>).</summary>
public class ContentSummaryDto
{
    public int TotalSkus { get; set; }
    public int WithContent { get; set; }
    public decimal CoveragePct { get; set; }
    public bool Subscribed { get; set; }
    public DateTime? LastSyncAt { get; set; }
}

/// <summary>Per-SKU content availability row (<c>GET /org/tenants/{id}/content</c>).</summary>
public class ContentRowDto
{
    public string Sku { get; set; } = string.Empty;
    public bool HasContent { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
}

/// <summary>Body of <c>POST /org/stock-check</c> — live SPR stock/price lookup for a tenant.</summary>
public class StockCheckRequest
{
    public string ExternalTenantId { get; set; } = string.Empty;
    public string ItemNumber { get; set; } = string.Empty;
    public List<int>? DcNumbers { get; set; }
    public bool AvailableOnly { get; set; } = true;
}

/// <summary>Response of <c>POST /org/stock-check</c>.</summary>
public class StockCheckResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ItemNumber { get; set; }
    public string? Upc { get; set; }
    public string? Description { get; set; }
    public string? ItemStatus { get; set; }
    public string? UnitOfMeasure { get; set; }
    public int? OrderMinimum { get; set; }
    public decimal? RetailPrice { get; set; }
    public string? HazmatMessage { get; set; }
    public bool PricingIncluded { get; set; }
    public decimal? DealerPrice { get; set; }
    public bool? Discountable { get; set; }
    public string? PriceDescription { get; set; }
    public List<DcAvailabilityDto> DistributionCenters { get; set; } = new();
}

public class DcAvailabilityDto
{
    public string DcNumber { get; set; } = string.Empty;
    public string? DcName { get; set; }
    public int Available { get; set; }
    public string? UnitOfMeasure { get; set; }
    public int? OnOrder { get; set; }
    public string? Expected { get; set; }
    public bool Sprinter { get; set; }
    public string? CutOff { get; set; }
    public string? LeadTime { get; set; }
    public string? DcType { get; set; }
}

/// <summary>Outcome of a live stock-check (distinguishes transport/no-connection failures from data).</summary>
public record StockCheckResult(bool Ok, string? Error, StockCheckResponse? Response)
{
    public static StockCheckResult Success(StockCheckResponse? r) => new(true, null, r);
    public static StockCheckResult Fail(string error) => new(false, error, null);
}
