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
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = "OrgAdmin";
}

/// <summary>Response of <c>POST /api/v1/org/auth/login</c> — the minted token + user/org summary.</summary>
public class OrgLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public OrgLoginUserDto User { get; set; } = new();
    public OrgLoginOrganizationDto Organization { get; set; } = new();
}

public class OrgLoginUserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class OrgLoginOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
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

// ============================================================================================
// Orders (increment 4) — tracking + document chain. Mirror the org API DTOs.
// ============================================================================================

/// <summary>A row in the Orders list (<c>GET /org/tenants/{id}/orders</c>).</summary>
public class OrderSummaryDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Document-chain stages present, ordered PO → ACK → ASN → Invoice.</summary>
    public List<string> Chain { get; set; } = new();

    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>Full order detail (<c>GET /org/tenants/{id}/orders/{id}</c>).</summary>
public class OrderDetailDto : OrderSummaryDto
{
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public string? PartnerOrderNumber { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
    public List<OrderDocumentDto> Documents { get; set; } = new();
    public List<string> Exceptions { get; set; } = new();
}

public class OrderLineDto
{
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? AcknowledgedQuantity { get; set; }
    public decimal? ShippedQuantity { get; set; }
    public decimal? BackorderedQuantity { get; set; }
}

/// <summary>A document in an order's chain. <see cref="ViewUrl"/> is null (metadata only) today.</summary>
public class OrderDocumentDto
{
    /// <summary>PO | ACK | ASN | Invoice.</summary>
    public string Type { get; set; } = string.Empty;
    public DateTime? ReceivedAt { get; set; }
    public int? Id { get; set; }
    public string? Reference { get; set; }
    public string? ViewUrl { get; set; }
}

/// <summary>A single entry in the tenant activity feed (<c>GET /org/tenants/{id}/activity</c>).</summary>
public class ActivityEventDto
{
    public DateTime At { get; set; }

    /// <summary>PriceFeed | Order | Connection | Exception.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Info | Warning | Error.</summary>
    public string Level { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? CorrelationId { get; set; }
    public string? Link { get; set; }
}

// ============================================================================================
// Organization admin (increment 5) — tenants, settings, dashboard summary. Mirror the org API DTOs.
// ============================================================================================

/// <summary>A tenant row for the Organization → Tenants screen (<c>GET /org/tenants</c>). Read-only.</summary>
public class OrgTenantRowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>The M360 mapping (external id); null until the tenant is provisioned by the operator.</summary>
    public string? ExternalId { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public int ConnectionCount { get; set; }
    public int ActiveConnectionCount { get; set; }
}

/// <summary>The org's editable profile (<c>GET/PUT /org/settings</c>). Never carries secrets.</summary>
public class OrgSettingsDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";

    /// <summary>False today — notification preferences aren't modeled; the page shows a note.</summary>
    public bool NotificationPreferencesSupported { get; set; }
}

/// <summary>Body of <c>PUT /org/settings</c> — editable org profile fields.</summary>
public class UpdateOrgSettingsRequest
{
    public string? Name { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

/// <summary>One-call dashboard summary for a tenant (<c>GET /org/tenants/{id}/summary</c>).</summary>
public class TenantSummaryDto
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public List<TenantConnectionHealthDto> Connections { get; set; } = new();
    public DateTime? LastPriceSyncAt { get; set; }
    public DateTime? LastContentSyncAt { get; set; }
    public int OpenErrorCount { get; set; }
    public List<OrderSummaryDto> RecentOrders { get; set; } = new();
}

public class TenantConnectionHealthDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastSyncAt { get; set; }
}

/// <summary>Outcome of a settings save (distinguishes validation/transport failures from success).</summary>
public record SettingsSaveResult(bool Success, string? Error, OrgSettingsDto? Settings)
{
    public static SettingsSaveResult Ok(OrgSettingsDto? s) => new(true, null, s);
    public static SettingsSaveResult Fail(string error) => new(false, error, null);
}
