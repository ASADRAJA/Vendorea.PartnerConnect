using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Org-facing API. An organization authenticates with its API key (the same key PartnerConnect
/// uses for outbound portal callbacks, supplied here as the inbound <c>X-Api-Key</c> header) and
/// can: see which partners it may connect to, request a tenant-partner connection on a tenant's
/// behalf, and list its connections. A "tenant initiates" by the org calling these endpoints with
/// the tenant's org-side id (ExternalTenantId).
/// </summary>
[ApiController]
[Route("api/v1/org")]
[Authorize] // Requires a valid API key; the caller must be an active organization (see ResolveOrgAsync).
public class OrgController : ControllerBase
{
    private const string ApiKeyHeader = "X-Api-Key";

    private readonly IOrgApiKeyAuthenticator _authenticator;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantConnectionService _connectionService;
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly ISprStockCheckService _stockCheckService;
    private readonly ISprFreightService _freightService;
    private readonly IPartnerDistributionCenterRepository _distributionCenterRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<OrgController> _logger;

    public OrgController(
        IOrgApiKeyAuthenticator authenticator,
        IOrganizationRepository organizationRepository,
        ITenantConnectionService connectionService,
        ITenantPartnerAccountRepository connectionRepository,
        ISprStockCheckService stockCheckService,
        ISprFreightService freightService,
        IPartnerDistributionCenterRepository distributionCenterRepository,
        ITenantRepository tenantRepository,
        ILogger<OrgController> logger)
    {
        _authenticator = authenticator;
        _organizationRepository = organizationRepository;
        _connectionService = connectionService;
        _connectionRepository = connectionRepository;
        _stockCheckService = stockCheckService;
        _freightService = freightService;
        _distributionCenterRepository = distributionCenterRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Current org + user context: org profile, the tenants the caller can access, role, and
    /// capabilities. Drives the customer portal's tenant switcher and nav gating. User identity
    /// and per-user capabilities are placeholders until the org user model lands (later increment).
    /// </summary>
    [HttpGet("me")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var tenants = await _tenantRepository.GetByOrganizationIdAsync(org.Id, cancellationToken);

        var dto = new OrgContextDto
        {
            Organization = new OrgContextOrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Status = org.Status.ToString()
            },
            // Per-user identity is a later increment; return a static OrgAdmin placeholder.
            User = new OrgContextUserDto
            {
                Id = null,
                DisplayName = null,
                Role = "OrgAdmin"
            },
            Tenants = tenants
                .OrderBy(t => t.Name)
                .Select(t => new OrgTenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ExternalId = t.ExternalId,
                    Status = t.Status.ToString()
                })
                .ToList(),
            Capabilities = new List<string> { "connections.read", "connections.write", "orders.read" }
        };

        return Ok(dto);
    }

    /// <summary>
    /// Live SPR stock/price check for one of the org's dealers. The dealer must have an active SPR
    /// connection; dealer-specific pricing is included automatically. Supply up to 8 DcNumbers for a
    /// lightweight per-DC check, or omit them for all stocking DCs.
    /// </summary>
    [HttpPost("stock-check")]
    [RequireScope(ApiScopes.StockRead)]
    public async Task<IActionResult> StockCheck([FromBody] StockCheckRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var outcome = await _stockCheckService.StockCheckAsync(org.Id, request, cancellationToken);
        return outcome.Status switch
        {
            StockCheckStatus.InvalidRequest => BadRequest(new { error = outcome.Error }),
            StockCheckStatus.NoActiveConnection => StatusCode(403, new { error = "Tenant has no active SPR connection" }),
            StockCheckStatus.NotConfigured => StatusCode(503, new { error = outcome.Error ?? "SPR web services are not configured" }),
            _ => Ok(outcome.Response)
        };
    }

    /// <summary>Live SPR freight rates (all qualifying UPS/FedEx options) for a dealer's shipment.</summary>
    [HttpPost("freight/rates")]
    [RequireScope(ApiScopes.FreightRead)]
    public async Task<IActionResult> FreightRates([FromBody] FreightRateRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;
        return FreightResult(await _freightService.FindRatesAsync(org.Id, request, cancellationToken));
    }

    /// <summary>Live SPR lowest freight rate for a dealer's shipment.</summary>
    [HttpPost("freight/lowest-rate")]
    [RequireScope(ApiScopes.FreightRead)]
    public async Task<IActionResult> LowestFreightRate([FromBody] FreightRateRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;
        return FreightResult(await _freightService.LowestRateAsync(org.Id, request, cancellationToken));
    }

    private IActionResult FreightResult(FreightOutcome outcome) => outcome.Status switch
    {
        FreightStatus.InvalidRequest => BadRequest(new { error = outcome.Error }),
        FreightStatus.NoActiveConnection => StatusCode(403, new { error = "Tenant has no active SPR connection" }),
        FreightStatus.NotConfigured => StatusCode(503, new { error = outcome.Error ?? "SPR web services are not configured" }),
        _ => Ok(outcome.Response)
    };

    /// <summary>Lists the trading partners this org may connect to, with each partner's required fields.</summary>
    [HttpGet("partners")]
    [RequireScope(ApiScopes.PartnersRead)]
    public async Task<IActionResult> GetPartners(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var withPartners = await _organizationRepository.GetByIdWithPartnersAsync(org.Id, cancellationToken);
        var partners = (withPartners?.Partners ?? new List<OrganizationPartner>())
            .Where(p => p.TradingPartner is not null && p.TradingPartner.Status == TradingPartnerStatus.Active)
            .Select(p => new OrgPartnerDto
            {
                TradingPartnerId = p.TradingPartnerId,
                Code = p.TradingPartner!.Code,
                Name = p.TradingPartner.Name,
                Description = p.TradingPartner.Description,
                RequiredFields = ParseFieldNames(p.TradingPartner.TenantConfirmationFieldsJson)
            })
            .ToList();

        return Ok(partners);
    }

    /// <summary>
    /// Lists a partner's distribution centers (reference/address data). The org must be associated
    /// with the partner and the partner must be active. No tenant connection is required — DC data
    /// is partner-level, not tenant- or pricing-specific.
    /// </summary>
    [HttpGet("partners/{partnerCode}/distribution-centers")]
    [RequireScope(ApiScopes.PartnersRead)]
    public async Task<IActionResult> GetPartnerDistributionCenters(string partnerCode, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var withPartners = await _organizationRepository.GetByIdWithPartnersAsync(org.Id, cancellationToken);
        var partner = (withPartners?.Partners ?? new List<OrganizationPartner>())
            .Select(p => p.TradingPartner)
            .FirstOrDefault(tp =>
                tp is not null &&
                tp.Status == TradingPartnerStatus.Active &&
                string.Equals(tp.Code, partnerCode, StringComparison.OrdinalIgnoreCase));

        // Don't leak partner existence to orgs that aren't associated with it.
        if (partner is null)
            return NotFound(new { error = $"Partner '{partnerCode}' not found for this organization" });

        var centers = await _distributionCenterRepository.GetByPartnerAsync(partner.Id, cancellationToken);

        var result = centers
            .OrderBy(dc => dc.DcNumber)
            .Select(dc => new PartnerDistributionCenterDto
            {
                DcNumber = dc.DcNumber,
                Label = dc.Label,
                Area = dc.Area,
                City = dc.City,
                State = dc.State,
                PostalCode = dc.PostalCode,
                Region = dc.Region,
                ContactName = dc.ContactName,
                AddressLine1 = dc.AddressLine1,
                AddressLine2 = dc.AddressLine2,
                Phone = dc.Phone,
                TollFreePhone = dc.TollFreePhone,
                Fax = dc.Fax,
                AdditionalContactInfo = dc.AdditionalContactInfo
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>Requests a tenant-partner connection (status Pending; tenant created on approval).</summary>
    [HttpPost("connections")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    public async Task<IActionResult> RequestConnection([FromBody] OrgConnectionRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var result = await _connectionService.RequestConnectionAsync(
            org.Id,
            new RequestConnectionInput(
                request.TradingPartnerId,
                request.ExternalTenantId,
                request.AccountNumber,
                request.ContactFirstName,
                request.ContactLastName,
                request.SpecialIdentifyingCode,
                request.Notes,
                request.ConfirmationFields),
            cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        _logger.LogInformation("Org {OrgId} requested connection {ConnectionId} for tenant {ExternalTenantId}",
            org.Id, result.Connection!.Id, request.ExternalTenantId);

        return Ok(MapConnection(result.Connection));
    }

    /// <summary>Lists this org's connection requests and their statuses.</summary>
    [HttpGet("connections")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connections = await _connectionRepository.GetConnectionsAsync(org.Id, null, cancellationToken);
        return Ok(connections.Select(MapConnection).ToList());
    }

    /// <summary>
    /// Detail for one of the org's connections: approval status + timestamps, partner capabilities,
    /// the editable/viewable configuration (partner account #, DC selection, preferences), a
    /// read-only transport summary (operator-managed at the partner level — never returns secrets),
    /// and a health summary. Returns 404 for a connection the org doesn't own (no enumeration).
    /// </summary>
    [HttpGet("connections/{id:int}")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetConnection(int id, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (connection is null || connection.OrganizationId != org.Id)
            return NotFound(new { error = $"Connection '{id}' not found" });

        return Ok(BuildDetailDto(connection));
    }

    /// <summary>
    /// Updates the editable configuration for a connection: partner account #, display name, the
    /// selected distribution centers (validated against the partner's DC list), and preferences.
    /// Transport is operator-managed at the partner level and is not editable here. Secrets are
    /// write-only and never returned.
    /// </summary>
    [HttpPut("connections/{id:int}")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    public async Task<IActionResult> UpdateConnection(int id, [FromBody] UpdateOrgConnectionRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (connection is null || connection.OrganizationId != org.Id)
            return NotFound(new { error = $"Connection '{id}' not found" });

        // Validate DC codes against the partner's published distribution centers (tolerate leading
        // zeros: the partner stores DcNumber as an int, the portal may send "0009").
        if (request.SelectedDistributionCenters is { Count: > 0 } dcs)
        {
            var centers = await _distributionCenterRepository.GetByPartnerAsync(connection.TradingPartnerId, cancellationToken);
            var valid = centers.Select(c => c.DcNumber).ToHashSet();
            var invalid = dcs
                .Where(code => !(int.TryParse(code, out var n) && valid.Contains(n)))
                .Distinct()
                .ToList();
            if (invalid.Count > 0)
                return UnprocessableEntity(new { error = $"Unknown distribution center code(s): {string.Join(", ", invalid)}" });
        }

        if (!string.IsNullOrWhiteSpace(request.PartnerAccountNumber))
            connection.AccountNumber = request.PartnerAccountNumber.Trim();

        if (request.DisplayName is not null)
            connection.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();

        // Merge customer-managed keys into ConfigurationJson without clobbering operator-set keys
        // (EnterpriseCode, ShipNode, etc.).
        var config = ParseConfigDict(connection.ConfigurationJson);
        var merged = new Dictionary<string, object?>(config.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in config)
            merged[kvp.Key] = kvp.Value;

        if (request.SelectedDistributionCenters is not null)
            merged["SelectedDistributionCenters"] = request.SelectedDistributionCenters;
        if (request.Preferences?.OrderType is not null)
            merged["OrderType"] = request.Preferences.OrderType;
        if (request.Preferences?.ContentSubscribed is not null)
            merged["ContentSubscribed"] = request.Preferences.ContentSubscribed.Value;

        connection.ConfigurationJson = JsonSerializer.Serialize(merged);

        // request.TransportPassword is accepted for forward-compatibility but intentionally NOT
        // applied: transport credentials are shared partner-level config managed by PC operators,
        // not per-connection secrets an org may set. (See TradingPartner.TransportCredentialsJson.)

        await _connectionRepository.UpdateAsync(connection, cancellationToken);
        _logger.LogInformation("Org {OrgId} updated connection {ConnectionId} configuration", org.Id, connection.Id);

        return Ok(BuildDetailDto(connection));
    }

    /// <summary>
    /// Suspends an approved connection: it's toggled inactive (orders/live services gate off) while
    /// remaining approved, so it can be resumed by an operator. Idempotent if already suspended.
    /// </summary>
    [HttpPost("connections/{id:int}/suspend")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    public async Task<IActionResult> SuspendConnection(int id, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (connection is null || connection.OrganizationId != org.Id)
            return NotFound(new { error = $"Connection '{id}' not found" });

        if (connection.ApprovalStatus != ConnectionApprovalStatus.Approved)
            return Conflict(new { error = "Only an approved connection can be suspended" });

        if (connection.IsActive)
        {
            connection.IsActive = false;
            await _connectionRepository.UpdateAsync(connection, cancellationToken);
            _logger.LogInformation("Org {OrgId} suspended connection {ConnectionId}", org.Id, connection.Id);
        }

        return Ok(BuildDetailDto(connection));
    }

    /// <summary>
    /// Disconnects a connection: a still-pending request is Cancelled, an approved connection is
    /// Unsubscribed (disabled). Both are terminal from the org's side. Idempotent if already
    /// disconnected.
    /// </summary>
    [HttpDelete("connections/{id:int}")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    public async Task<IActionResult> DisconnectConnection(int id, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (connection is null || connection.OrganizationId != org.Id)
            return NotFound(new { error = $"Connection '{id}' not found" });

        var changed = connection.ApprovalStatus switch
        {
            ConnectionApprovalStatus.Pending => Transition(connection, ConnectionApprovalStatus.Cancelled),
            ConnectionApprovalStatus.Approved => Transition(connection, ConnectionApprovalStatus.Unsubscribed),
            _ => false // already terminal (Denied/Cancelled/Unsubscribed) — idempotent
        };

        if (changed)
        {
            await _connectionRepository.UpdateAsync(connection, cancellationToken);
            _logger.LogInformation("Org {OrgId} disconnected connection {ConnectionId} ({Status})",
                org.Id, connection.Id, connection.ApprovalStatus);
        }

        return Ok(BuildDetailDto(connection));
    }

    private static bool Transition(TenantPartnerAccount connection, ConnectionApprovalStatus status)
    {
        connection.ApprovalStatus = status;
        connection.IsActive = false;
        connection.DecidedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>Resolves the calling org from its X-Api-Key, or returns a 401 result.</summary>
    private async Task<(Organization? Org, IActionResult? Error)> ResolveOrgAsync(CancellationToken cancellationToken)
    {
        var apiKey = Request.Headers[ApiKeyHeader].FirstOrDefault();
        var org = await _authenticator.ResolveActiveOrganizationAsync(apiKey, cancellationToken);
        if (org is null)
            return (null, Unauthorized(new { error = "Invalid or missing API key" }));

        return (org, null);
    }

    private static OrgConnectionDto MapConnection(TenantPartnerAccount c) => new()
    {
        Id = c.Id,
        TradingPartnerId = c.TradingPartnerId,
        PartnerName = c.TradingPartner?.Name,
        ExternalTenantId = c.ExternalTenantId,
        AccountNumber = c.AccountNumber,
        ApprovalStatus = c.ApprovalStatus.ToString(),
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        DecidedAt = c.DecidedAt
    };

    private static List<string> ParseFieldNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    /// <summary>Builds the connection-detail DTO from a fully-loaded connection (partner + capabilities).</summary>
    private static OrgConnectionDetailDto BuildDetailDto(TenantPartnerAccount c)
    {
        var partner = c.TradingPartner;
        var config = ParseConfigDict(c.ConfigurationJson);

        return new OrgConnectionDetailDto
        {
            Id = c.Id,
            PartnerCode = partner?.Code ?? string.Empty,
            PartnerName = partner?.Name ?? $"Partner {c.TradingPartnerId}",
            TradingPartnerId = c.TradingPartnerId,
            TenantId = c.TenantId,
            ExternalTenantId = c.ExternalTenantId,
            Status = MapStatus(c),
            ApprovalStatus = c.ApprovalStatus.ToString(),
            RequestedAt = c.CreatedAt,
            ApprovedAt = c.ApprovalStatus == ConnectionApprovalStatus.Approved ? c.DecidedAt : null,
            DecidedAt = c.DecidedAt,
            DecisionReason = c.DecisionReason,
            Capabilities = MapCapabilities(partner),
            Config = new OrgConnectionConfigDto
            {
                PartnerAccountNumber = c.AccountNumber,
                DisplayName = c.DisplayName,
                SelectedDistributionCenters = GetConfigStringList(config, "SelectedDistributionCenters"),
                Preferences = new OrgConnectionPreferencesDto
                {
                    OrderType = GetConfigString(config, "OrderType"),
                    ContentSubscribed = GetConfigBool(config, "ContentSubscribed")
                },
                Transport = BuildTransportDto(partner)
            },
            Health = new OrgConnectionHealthDto
            {
                LastSyncAt = c.LastUsedAt ?? c.VerifiedAt,
                LastError = null // per-connection error tracking not modeled yet
            }
        };
    }

    /// <summary>
    /// Portal-facing status: derived from approval state + IsActive. Approved-but-inactive reads as
    /// "Suspended"; Unsubscribed reads as "Disconnected".
    /// </summary>
    private static string MapStatus(TenantPartnerAccount c) => c.ApprovalStatus switch
    {
        ConnectionApprovalStatus.Pending => "Pending",
        ConnectionApprovalStatus.Approved => c.IsActive ? "Active" : "Suspended",
        ConnectionApprovalStatus.Unsubscribed => "Disconnected",
        ConnectionApprovalStatus.Cancelled => "Cancelled",
        ConnectionApprovalStatus.Denied => "Denied",
        _ => c.ApprovalStatus.ToString()
    };

    private static List<string> MapCapabilities(TradingPartner? partner)
    {
        if (partner?.Capabilities is null)
            return new List<string>();

        return partner.Capabilities
            .Where(cap => cap.IsEnabled)
            .Select(cap => cap.Capability switch
            {
                PartnerCapability.PriceFeed => "prices",
                PartnerCapability.InventoryFeed => "inventory",
                PartnerCapability.ProductContent => "content",
                PartnerCapability.OrderSubmission => "orders",
                PartnerCapability.OrderStatusUpdates => "orders",
                PartnerCapability.InvoiceReceive => "invoices",
                PartnerCapability.ShipmentTracking => "shipments",
                PartnerCapability.ReturnProcessing => "returns",
                PartnerCapability.CatalogSync => "catalog",
                _ => cap.Capability.ToString().ToLowerInvariant()
            })
            .Distinct()
            .ToList();
    }

    /// <summary>Read-only transport summary (operator-managed at the partner level). Never returns secrets.</summary>
    private static OrgConnectionTransportDto BuildTransportDto(TradingPartner? partner)
    {
        var cfg = ParseConfigDict(partner?.TransportConfigJson);
        return new OrgConnectionTransportDto
        {
            Type = GetConfigString(cfg, "Type") ?? GetConfigString(cfg, "TransportType"),
            Host = GetConfigString(cfg, "Host"),
            Username = GetConfigString(cfg, "Username") ?? GetConfigString(cfg, "User"),
            HasPassword = !string.IsNullOrWhiteSpace(partner?.TransportCredentialsJson),
            Editable = false,
            ManagedByOperator = true
        };
    }

    private static Dictionary<string, JsonElement> ParseConfigDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return raw is null
                ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, JsonElement>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? GetConfigString(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool? GetConfigBool(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;

    private static List<string> GetConfigStringList(Dictionary<string, JsonElement> d, string key)
    {
        if (d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Array)
            return v.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        return new List<string>();
    }
}

public class OrgPartnerDto
{
    public int TradingPartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> RequiredFields { get; set; } = new();
}

public class OrgConnectionRequest
{
    public int TradingPartnerId { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? SpecialIdentifyingCode { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string>? ConfirmationFields { get; set; }
}

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
}

/// <summary>Current org + user context returned by <c>GET /api/v1/org/me</c>.</summary>
public class OrgContextDto
{
    public OrgContextOrganizationDto Organization { get; set; } = new();
    public OrgContextUserDto User { get; set; } = new();
    public List<OrgTenantDto> Tenants { get; set; } = new();
    public List<string> Capabilities { get; set; } = new();
}

public class OrgContextOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class OrgContextUserDto
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

/// <summary>Detail for a single connection, returned by <c>GET /api/v1/org/connections/{id}</c>.</summary>
public class OrgConnectionDetailDto
{
    public int Id { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public int TradingPartnerId { get; set; }
    public int? TenantId { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;

    /// <summary>Portal-facing status: Pending | Active | Suspended | Disconnected | Cancelled | Denied.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Raw domain approval state (Pending | Approved | Denied | Cancelled | Unsubscribed).</summary>
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

/// <summary>Read-only transport summary. Secrets are never returned — only <see cref="HasPassword"/>.</summary>
public class OrgConnectionTransportDto
{
    public string? Type { get; set; }
    public string? Host { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }

    /// <summary>False: transport is shared partner-level config, editable by PC operators only.</summary>
    public bool Editable { get; set; }
    public bool ManagedByOperator { get; set; } = true;
}

public class OrgConnectionHealthDto
{
    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Body of <c>PUT /api/v1/org/connections/{id}</c> — the editable configuration.</summary>
public class UpdateOrgConnectionRequest
{
    public string? PartnerAccountNumber { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>DC codes to select. Validated against the partner's published DC list.</summary>
    public List<string>? SelectedDistributionCenters { get; set; }

    public OrgConnectionPreferencesDto? Preferences { get; set; }

    /// <summary>
    /// Write-only, reserved. Transport credentials are operator-managed at the partner level, so
    /// this is accepted for forward-compatibility but not applied today.
    /// </summary>
    public string? TransportPassword { get; set; }
}
