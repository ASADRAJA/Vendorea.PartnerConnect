using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authentication;
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
    private readonly ISprPriceRecordRepository _priceRepository;
    private readonly ISupplierInventoryItemRepository _inventoryRepository;
    private readonly IDealerContentSubscriptionRepository _contentSubscriptionRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IDocumentCorrelationRepository _correlationRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ITenantSummaryRepository _summaryRepository;
    private readonly ILogger<OrgController> _logger;

    /// <summary>Default content locale for coverage/availability lookups.</summary>
    private const string DefaultLocaleId = "EN_US";

    public OrgController(
        IOrgApiKeyAuthenticator authenticator,
        IOrganizationRepository organizationRepository,
        ITenantConnectionService connectionService,
        ITenantPartnerAccountRepository connectionRepository,
        ISprStockCheckService stockCheckService,
        ISprFreightService freightService,
        IPartnerDistributionCenterRepository distributionCenterRepository,
        ITenantRepository tenantRepository,
        ISprPriceRecordRepository priceRepository,
        ISupplierInventoryItemRepository inventoryRepository,
        IDealerContentSubscriptionRepository contentSubscriptionRepository,
        IOrderRepository orderRepository,
        IDocumentCorrelationRepository correlationRepository,
        IActivityRepository activityRepository,
        ITenantSummaryRepository summaryRepository,
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
        _priceRepository = priceRepository;
        _inventoryRepository = inventoryRepository;
        _contentSubscriptionRepository = contentSubscriptionRepository;
        _orderRepository = orderRepository;
        _correlationRepository = correlationRepository;
        _activityRepository = activityRepository;
        _summaryRepository = summaryRepository;
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

        // When resolved via a user token, return the real user and restrict tenants to the user's
        // scope. When resolved via the org API key (integration), keep the static OrgAdmin/all-tenants
        // behavior.
        IEnumerable<Tenant> visibleTenants = tenants;
        OrgContextUserDto userDto;
        List<string> capabilities;

        if (_resolvedUser is { } u)
        {
            if (!u.AllTenants)
                visibleTenants = tenants.Where(t => u.ScopedTenantIds.Contains(t.Id));

            userDto = new OrgContextUserDto
            {
                Id = u.UserId?.ToString(),
                DisplayName = u.DisplayName,
                Email = u.Email,
                Role = u.Role.ToString()
            };
            capabilities = CapabilitiesForRole(u.Role);
        }
        else
        {
            // Org-API-key (integration) path: no per-user identity → full org-admin access, as today.
            userDto = new OrgContextUserDto
            {
                Id = null,
                DisplayName = null,
                Email = null,
                Role = "OrgAdmin"
            };
            capabilities = CapabilitiesForRole(OrgPortalRole.OrgAdmin);
        }

        var dto = new OrgContextDto
        {
            Organization = new OrgContextOrganizationDto
            {
                Id = org.Id,
                Name = org.Name,
                Status = org.Status.ToString()
            },
            User = userDto,
            Tenants = visibleTenants
                .OrderBy(t => t.Name)
                .Select(t => new OrgTenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ExternalId = t.ExternalId,
                    Status = t.Status.ToString()
                })
                .ToList(),
            Capabilities = capabilities
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

    // ============================================================================================
    // Catalog (read-only, tenant-scoped): prices, inventory, content.
    // tenantId is PC's internal Tenant.Id, which is also the DealerId on price/content records.
    // Prices and content are per-dealer(tenant)+partner; inventory is partner-level (shared across
    // the partner's connected tenants). Every tenantId is association-gated to the calling org.
    // ============================================================================================

    /// <summary>
    /// Lists the partners the given tenant has an approved connection to. Drives the catalog pages'
    /// partner selector. Returns 404 if the tenant isn't the org's.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/partners")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetTenantPartners(int tenantId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return tenantError!;

        var partners = await GetConnectedPartnersAsync(org.Id, tenantId, cancellationToken);
        return Ok(partners
            .Select(p => new OrgCatalogPartnerDto
            {
                PartnerCode = p.TradingPartner!.Code,
                PartnerName = p.TradingPartner.Name,
                Capabilities = MapCapabilities(p.TradingPartner)
            })
            .ToList());
    }

    /// <summary>
    /// Current prices for the tenant's connection with a partner (paged, searchable by SKU or
    /// description). Cost/list/UOM come from the latest completed price feed for the tenant.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/prices")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetPrices(
        int tenantId,
        [FromQuery] string? partnerCode,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (connection, resolveError) = await ResolveTenantPartnerAsync(org, tenantId, partnerCode, cancellationToken);
        if (resolveError is not null)
            return resolveError;
        if (connection is null)
            return Ok(PagedResult<PriceRowDto>.Empty(skip, take)); // no connected partner → empty

        (skip, take) = NormalizePaging(skip, take);
        var page = await _priceRepository.GetCurrentPricePageAsync(
            tenantId, connection.TradingPartner!.Code, search, skip, take, cancellationToken);

        var items = page.Items.Select(r => new PriceRowDto
        {
            Sku = r.Sku,
            Description = r.Description,
            Cost = r.Cost,
            ListPrice = r.ListPrice,
            Uom = r.Uom,
            EffectiveDate = r.EffectiveDate,
            LastUpdatedAt = r.LastUpdatedAt
        }).ToList();

        return Ok(new PagedResult<PriceRowDto>(items, page.Total, skip, take));
    }

    /// <summary>
    /// Price history for a single SKU: one point per completed price feed that contained it, newest
    /// first. If the partner keeps only a single (current) feed, this returns just that record.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/prices/{sku}/history")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetPriceHistory(
        int tenantId,
        string sku,
        [FromQuery] string? partnerCode,
        CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (connection, resolveError) = await ResolveTenantPartnerAsync(org, tenantId, partnerCode, cancellationToken);
        if (resolveError is not null)
            return resolveError;
        if (connection is null)
            return Ok(new PriceHistoryDto { Sku = sku });

        var history = await _priceRepository.GetPriceHistoryAsync(
            tenantId, connection.TradingPartner!.Code, sku, 50, cancellationToken);

        return Ok(new PriceHistoryDto
        {
            Sku = sku,
            Points = history.Select(h => new PriceHistoryPointDto
            {
                Cost = h.Cost,
                ListPrice = h.ListPrice,
                Uom = h.Uom,
                EffectiveDate = h.EffectiveDate,
                EndDate = h.EndDate,
                UpdatedAt = h.UploadedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Stock by distribution center for the tenant's partner (paged, searchable). Inventory is
    /// partner-level shared data — the same snapshot serves all the partner's connected tenants, so
    /// the response is flagged <c>partnerLevel: true</c>.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/inventory")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetInventory(
        int tenantId,
        [FromQuery] string? partnerCode,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (connection, resolveError) = await ResolveTenantPartnerAsync(org, tenantId, partnerCode, cancellationToken);
        if (resolveError is not null)
            return resolveError;
        if (connection is null)
            return Ok(PagedResult<InventoryRowDto>.Empty(skip, take));

        (skip, take) = NormalizePaging(skip, take);
        var page = await _inventoryRepository.SearchCurrentInventoryAsync(
            connection.TradingPartnerId, search, skip, take, cancellationToken);

        var items = page.Items.Select(i => new InventoryRowDto
        {
            Sku = i.SupplierSku,
            Description = i.Description,
            Uom = i.UnitOfMeasure,
            AsOf = page.AsOf,
            ByDistributionCenter = i.LocationQuantities
                .OrderBy(l => l.LocationCode)
                .Select(l =>
                {
                    var onHand = l.QuantityOnHand ?? l.QuantityAvailable;
                    return new InventoryDcDto
                    {
                        Dc = l.LocationCode,
                        DcName = l.LocationName,
                        OnHand = onHand,
                        Status = onHand > 0 ? "InStock" : "OutOfStock"
                    };
                })
                .ToList()
        }).ToList();

        return Ok(new PagedResult<InventoryRowDto>(items, page.Total, skip, take)
        {
            PartnerLevel = true
        });
    }

    /// <summary>
    /// Content coverage summary for the tenant + partner: how many of the tenant's catalog SKUs have
    /// shared partner content, and the tenant's content subscription state (view-only).
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/content/summary")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetContentSummary(
        int tenantId,
        [FromQuery] string? partnerCode,
        CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (connection, resolveError) = await ResolveTenantPartnerAsync(org, tenantId, partnerCode, cancellationToken);
        if (resolveError is not null)
            return resolveError;
        if (connection is null)
            return Ok(new ContentSummaryDto());

        var coverage = await _priceRepository.GetContentCoverageAsync(
            tenantId, connection.TradingPartner!.Code, DefaultLocaleId, cancellationToken);

        var subscription = await _contentSubscriptionRepository.GetByDealerAndPartnerAsync(
            tenantId, connection.TradingPartnerId, cancellationToken);

        return Ok(new ContentSummaryDto
        {
            TotalSkus = coverage.TotalSkus,
            WithContent = coverage.WithContent,
            CoveragePct = coverage.TotalSkus > 0
                ? Math.Round((decimal)coverage.WithContent * 100 / coverage.TotalSkus, 1)
                : 0,
            Subscribed = subscription?.IsEnhancedContentEnabled ?? false,
            LastSyncAt = subscription?.LastFullRefreshAt
        });
    }

    /// <summary>
    /// Per-SKU content availability for the tenant's catalog (paged, searchable): whether each SKU
    /// has shared partner content, with the brand/description when it does.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/content")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetContent(
        int tenantId,
        [FromQuery] string? partnerCode,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (connection, resolveError) = await ResolveTenantPartnerAsync(org, tenantId, partnerCode, cancellationToken);
        if (resolveError is not null)
            return resolveError;
        if (connection is null)
            return Ok(PagedResult<ContentRowDto>.Empty(skip, take));

        (skip, take) = NormalizePaging(skip, take);
        var page = await _priceRepository.GetSkuContentPageAsync(
            tenantId, connection.TradingPartner!.Code, DefaultLocaleId, search, skip, take, cancellationToken);

        var items = page.Items.Select(r => new ContentRowDto
        {
            Sku = r.Sku,
            HasContent = r.HasContent,
            Brand = r.Brand,
            Description = r.ContentDescription ?? r.Description
        }).ToList();

        return Ok(new PagedResult<ContentRowDto>(items, page.Total, skip, take));
    }

    // ============================================================================================
    // Orders (read-only tracking) + Activity feed. Both are tenant-scoped and association-gated.
    // tenantId is PC's internal Tenant.Id (== Order.TenantId).
    // ============================================================================================

    /// <summary>
    /// Combined, org-level order list across every tenant the caller can see (an OrgAdmin sees all the
    /// org's tenants; a scoped user sees only their tenants). Each row carries the tenant that placed
    /// the order. Optional <paramref name="tenantId"/> narrows to a single in-scope tenant. Filterable
    /// by status + order-date range, paged, newest first.
    /// </summary>
    [HttpGet("orders")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetOrgOrders(
        [FromQuery] int? tenantId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        // Tenants this caller may see: all the org's, or the scoped subset for a tenant-scoped user.
        var tenants = await _tenantRepository.GetByOrganizationIdAsync(org.Id, cancellationToken);
        var visibleIds = tenants
            .Where(t => _resolvedUser is not { AllTenants: false } || _resolvedUser.ScopedTenantIds.Contains(t.Id))
            .Select(t => t.Id);
        if (tenantId is int tid)
            visibleIds = visibleIds.Where(id => id == tid); // narrow to one in-scope tenant
        var idList = visibleIds.ToList();
        if (idList.Count == 0)
            return Ok(PagedResult<OrderSummaryDto>.Empty(skip, take));

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                return Ok(PagedResult<OrderSummaryDto>.Empty(skip, take));
            statusFilter = parsed;
        }

        (skip, take) = NormalizePaging(skip, take);
        var (items, total) = await _orderRepository.GetTenantsOrderPageAsync(
            idList, null, statusFilter, from, to, skip, take, cancellationToken);

        var dtos = items.Select(o => new OrderSummaryDto
        {
            Id = o.Id,
            PoNumber = o.PoNumber,
            TenantId = o.TenantId,
            TenantName = o.Tenant?.Name ?? $"Tenant {o.TenantId}",
            PartnerCode = o.TradingPartner?.Code ?? string.Empty,
            PartnerName = o.TradingPartner?.Name ?? $"Partner {o.TradingPartnerId}",
            OrderedAt = o.OrderDate,
            Status = o.Status.ToString(),
            Chain = DeriveChain(o),
            Total = o.TotalAmount,
            Currency = o.Currency
        }).ToList();

        return Ok(new PagedResult<OrderSummaryDto>(dtos, total, skip, take));
    }

    /// <summary>
    /// Full detail for one order by id, for the combined view — resolves the order's tenant and reuses
    /// the tenant-scoped detail path, which enforces org ownership + per-user tenant scope. Returns 404
    /// if the order's tenant is outside the caller's scope.
    /// </summary>
    [HttpGet("orders/{orderId:int}")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetOrgOrder(int orderId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
            return NotFound(new { error = $"Order '{orderId}' not found" });

        return await GetOrder(order.TenantId, orderId, cancellationToken);
    }

    /// <summary>
    /// Lists the tenant's orders (newest first), filterable by partner, status, and order-date
    /// range, paged. Each row carries the document-chain stages present (PO/ACK/ASN) derived from the
    /// order's own state — cheap and authoritative, no per-row document lookup. Returns 404 if the
    /// tenant isn't the org's.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/orders")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetOrders(
        int tenantId,
        [FromQuery] string? partnerCode,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return tenantError!;

        // Resolve the optional partner filter to a trading-partner id via the tenant's connections.
        int? tradingPartnerId = null;
        if (!string.IsNullOrWhiteSpace(partnerCode))
        {
            var partners = await GetConnectedPartnersAsync(org.Id, tenantId, cancellationToken);
            var match = partners.FirstOrDefault(p =>
                string.Equals(p.TradingPartner!.Code, partnerCode, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return Ok(PagedResult<OrderSummaryDto>.Empty(skip, take)); // unknown/unconnected partner
            tradingPartnerId = match.TradingPartnerId;
        }

        // Unknown status value → strictly empty (rather than silently ignoring the filter).
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                return Ok(PagedResult<OrderSummaryDto>.Empty(skip, take));
            statusFilter = parsed;
        }

        (skip, take) = NormalizePaging(skip, take);
        var (items, total) = await _orderRepository.GetTenantOrderPageAsync(
            tenantId, tradingPartnerId, statusFilter, from, to, skip, take, cancellationToken);

        var dtos = items.Select(o => new OrderSummaryDto
        {
            Id = o.Id,
            PoNumber = o.PoNumber,
            PartnerCode = o.TradingPartner?.Code ?? string.Empty,
            PartnerName = o.TradingPartner?.Name ?? $"Partner {o.TradingPartnerId}",
            OrderedAt = o.OrderDate,
            Status = o.Status.ToString(),
            Chain = DeriveChain(o),
            Total = o.TotalAmount,
            Currency = o.Currency
        }).ToList();

        return Ok(new PagedResult<OrderSummaryDto>(dtos, total, skip, take));
    }

    /// <summary>
    /// Full detail for one of the tenant's orders: header + line items + the assembled document
    /// chain (PO → ACK → ASN → Invoice) and any exceptions. The chain is assembled from document
    /// correlation (by PO number) and back-filled from the order's own lifecycle timestamps, so the
    /// stepper is populated even before inbound partner documents are correlated. Returns 404 for a
    /// tenant/order the org doesn't own.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/orders/{orderId:int}")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetOrder(int tenantId, int orderId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return tenantError!;

        var order = await _orderRepository.GetByIdWithFullDetailsAsync(orderId, cancellationToken);
        if (order is null || order.TenantId != tenantId)
            return NotFound(new { error = $"Order '{orderId}' not found" });

        var (documents, exceptions) = await AssembleDocumentChainAsync(order, tenantId, cancellationToken);

        var dto = new OrderDetailDto
        {
            Id = order.Id,
            PoNumber = order.PoNumber,
            PartnerCode = order.TradingPartner?.Code ?? string.Empty,
            PartnerName = order.TradingPartner?.Name ?? $"Partner {order.TradingPartnerId}",
            OrderedAt = order.OrderDate,
            Status = order.Status.ToString(),
            Chain = documents.Select(d => d.Type).ToList(),
            Total = order.TotalAmount,
            Currency = order.Currency,
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            PartnerOrderNumber = order.PartnerOrderNumber,
            SubmittedAt = order.SubmittedAt,
            AcknowledgedAt = order.AcknowledgedAt,
            ShippedAt = order.ShippedAt,
            CompletedAt = order.CompletedAt,
            Notes = order.Notes,
            Lines = order.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new OrderLineDto
                {
                    LineNumber = l.LineNumber,
                    Sku = l.Sku,
                    VendorSku = l.VendorSku,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitOfMeasure = l.UnitOfMeasure,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    Status = l.Status.ToString(),
                    AcknowledgedQuantity = l.AcknowledgedQuantity,
                    ShippedQuantity = l.ShippedQuantity,
                    BackorderedQuantity = l.BackorderedQuantity
                })
                .ToList(),
            Documents = documents,
            Exceptions = exceptions
        };

        return Ok(dto);
    }

    /// <summary>
    /// Unified, tenant-scoped activity feed (price-feed uploads, order status changes, connection
    /// state, quarantined documents), filterable by type/level/date and paged. Returns 404 if the
    /// tenant isn't the org's.
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/activity")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetActivity(
        int tenantId,
        [FromQuery] string? type,
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return tenantError!;

        (skip, take) = NormalizePaging(skip, take);
        var (items, total) = await _activityRepository.GetTenantActivityPageAsync(
            tenantId, type, level, from, to, skip, take, cancellationToken);

        var dtos = items.Select(e => new ActivityEventDto
        {
            At = e.At,
            Type = e.Type,
            Level = e.Level,
            Title = e.Title,
            Detail = e.Detail,
            CorrelationId = e.CorrelationId,
            Link = e.Link
        }).ToList();

        return Ok(new PagedResult<ActivityEventDto>(dtos, total, skip, take));
    }

    // ============================================================================================
    // Organization admin (increment 5): tenants, settings, and the dashboard summary.
    // Tenants are operator/M360-provisioned (created on connection approval), so they're read-only
    // here. Per-user identity isn't wired to the org portal yet, so there is no /org/users endpoint.
    // ============================================================================================

    /// <summary>
    /// The organization's tenants (read-only). Tenants are provisioned by PC operators / M360 when a
    /// connection is approved — the portal cannot create them — so each row also carries the M360
    /// mapping (ExternalId) and a cheap connection roll-up for the Organization → Tenants screen.
    /// </summary>
    [HttpGet("tenants")]
    [RequireScope(ApiScopes.OrgAdmin)] // Org-admin-only: the tenant admin roll-up (Organization → Tenants).
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var tenants = await _tenantRepository.GetByOrganizationIdAsync(org.Id, cancellationToken);

        // One connection fetch for the whole org; roll up per tenant in memory (org-sized, not a scan).
        var connections = await _connectionRepository.GetConnectionsAsync(org.Id, null, cancellationToken);
        var byTenant = connections
            .Where(c => c.TenantId.HasValue)
            .GroupBy(c => c.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = tenants
            .OrderBy(t => t.Name)
            .Select(t =>
            {
                byTenant.TryGetValue(t.Id, out var conns);
                conns ??= new List<TenantPartnerAccount>();
                return new OrgTenantRowDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Code = t.Code,
                    ExternalId = t.ExternalId,
                    Status = t.Status.ToString(),
                    ContactName = JoinName(t.ContactFirstName, t.ContactLastName),
                    ContactEmail = t.ContactEmail,
                    ConnectionCount = conns.Count,
                    ActiveConnectionCount = conns.Count(c =>
                        c.ApprovalStatus == ConnectionApprovalStatus.Approved && c.IsActive)
                };
            })
            .ToList();

        return Ok(rows);
    }

    /// <summary>
    /// The organization's editable profile (contact + address). Identity fields (code, status) and
    /// the API key are never editable here and the key is never returned. Notification preferences
    /// are not modeled yet — the customer portal surfaces that as a note, not a fake control.
    /// </summary>
    [HttpGet("settings")]
    [RequireScope(ApiScopes.OrgAdmin)] // Org-admin-only: the editable org profile.
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        return Ok(MapSettings(org));
    }

    /// <summary>
    /// Updates the organization's editable profile fields (name, contact, address). Only real,
    /// non-secret fields are accepted; code, status, billing, and credentials are ignored. A null
    /// field leaves the stored value unchanged; an empty string clears an optional field.
    /// </summary>
    [HttpPut("settings")]
    [RequireScope(ApiScopes.OrgAdmin)] // Org-admin-only: writing the org profile.
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateOrgSettingsRequest request, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
                return UnprocessableEntity(new { error = "Organization name cannot be empty" });
            org.Name = name;
        }

        if (request.ContactEmail is not null)
            org.ContactEmail = Clean(request.ContactEmail);
        if (request.ContactPhone is not null)
            org.ContactPhone = Clean(request.ContactPhone);
        if (request.Address is not null)
            org.Address = Clean(request.Address);
        if (request.City is not null)
            org.City = Clean(request.City);
        if (request.State is not null)
            org.State = Clean(request.State);
        if (request.PostalCode is not null)
            org.PostalCode = Clean(request.PostalCode);
        if (request.Country is not null)
        {
            var country = request.Country.Trim();
            org.Country = country.Length == 0 ? "US" : country;
        }

        org.UpdatedAt = DateTime.UtcNow;
        await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Org {OrgId} updated its profile settings", org.Id);

        return Ok(MapSettings(org));
    }

    /// <summary>
    /// One-call dashboard summary for a tenant: connection health per partner, last successful price
    /// and content sync, the five most recent orders, and the open-error count. Association-gated to
    /// the calling org (404 for a tenant it doesn't own).
    /// </summary>
    [HttpGet("tenants/{tenantId:int}/summary")]
    [RequireScope(ApiScopes.ConnectionsRead)]
    public async Task<IActionResult> GetTenantSummary(int tenantId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return tenantError!;

        // Connection health: the tenant's connections (any state), one row per partner, newest wins.
        var connections = await _connectionRepository.GetConnectionsAsync(org.Id, null, cancellationToken);
        var health = connections
            .Where(c => c.TenantId == tenantId && c.TradingPartner is not null)
            .GroupBy(c => c.TradingPartnerId)
            .Select(g => g.OrderByDescending(c => c.DecidedAt ?? c.CreatedAt).First())
            .OrderBy(c => c.TradingPartner!.Name)
            .Select(c => new TenantConnectionHealthDto
            {
                PartnerCode = c.TradingPartner!.Code,
                PartnerName = c.TradingPartner.Name,
                Status = MapStatus(c),
                LastSyncAt = c.LastUsedAt ?? c.VerifiedAt
            })
            .ToList();

        var (recentOrders, _) = await _orderRepository.GetTenantOrderPageAsync(
            tenantId, null, null, null, null, 0, 5, cancellationToken);

        var recent = recentOrders.Select(o => new OrderSummaryDto
        {
            Id = o.Id,
            PoNumber = o.PoNumber,
            PartnerCode = o.TradingPartner?.Code ?? string.Empty,
            PartnerName = o.TradingPartner?.Name ?? $"Partner {o.TradingPartnerId}",
            OrderedAt = o.OrderDate,
            Status = o.Status.ToString(),
            Chain = DeriveChain(o),
            Total = o.TotalAmount,
            Currency = o.Currency
        }).ToList();

        var signals = await _summaryRepository.GetSummarySignalsAsync(tenantId, cancellationToken);

        return Ok(new TenantSummaryDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            Connections = health,
            LastPriceSyncAt = signals.LastPriceSyncAt,
            LastContentSyncAt = signals.LastContentSyncAt,
            OpenErrorCount = signals.OpenErrorCount,
            RecentOrders = recent
        });
    }

    private OrgSettingsDto MapSettings(Organization org) => new()
    {
        Id = org.Id,
        Code = org.Code,
        Name = org.Name,
        Status = org.Status.ToString(),
        ContactEmail = org.ContactEmail,
        ContactPhone = org.ContactPhone,
        Address = org.Address,
        City = org.City,
        State = org.State,
        PostalCode = org.PostalCode,
        Country = org.Country,
        // Notification preferences aren't modeled yet — advertised to the portal so it can render an
        // honest note rather than an inert control.
        NotificationPreferencesSupported = false
    };

    private static string? JoinName(string? first, string? last)
    {
        var name = string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? Clean(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Stage ordering for the document chain, so it always reads PO → ACK → ASN → Invoice.</summary>
    private static int StageRank(string type) => type switch
    {
        "PO" => 0,
        "ACK" => 1,
        "ASN" => 2,
        "Invoice" => 3,
        _ => 99
    };

    /// <summary>
    /// The chain stages present for an order, derived from its own lifecycle state (cheap, no
    /// document lookup). PO is always present (the order is the PO); ACK/ASN follow the order's
    /// acknowledgment/shipment signals. Invoice can't be derived from the order and is only surfaced
    /// in the detail view (from correlated documents).
    /// </summary>
    private static List<string> DeriveChain(Order o)
    {
        var chain = new List<string> { "PO" };

        var acknowledged = o.AcknowledgedAt.HasValue
            || o.AcknowledgmentDocumentId.HasValue
            || o.Status is OrderStatus.Acknowledged or OrderStatus.Processing
                or OrderStatus.PartiallyShipped or OrderStatus.Shipped
                or OrderStatus.Delivered or OrderStatus.Completed;
        if (acknowledged)
            chain.Add("ACK");

        var shipped = o.ShippedAt.HasValue
            || o.Status is OrderStatus.PartiallyShipped or OrderStatus.Shipped
                or OrderStatus.Delivered or OrderStatus.Completed;
        if (shipped)
            chain.Add("ASN");

        return chain;
    }

    /// <summary>
    /// Assembles an order's document chain and exceptions. Real <see cref="PartnerDocument"/>s
    /// correlated by PO number are the primary source (they carry Invoice + received-at + a
    /// reference); the order's lifecycle timestamps back-fill any PO/ACK/ASN stage not yet present as
    /// a correlated document. Exceptions are drawn from the order's own failure and from any
    /// correlated document in a failed state. No document download URL is exposed (there is no
    /// org-scoped document endpoint), so <c>ViewUrl</c> is null — metadata only.
    /// </summary>
    private async Task<(List<OrderDocumentDto> Documents, List<string> Exceptions)> AssembleDocumentChainAsync(
        Order order, int tenantId, CancellationToken cancellationToken)
    {
        var byType = new Dictionary<string, OrderDocumentDto>(StringComparer.Ordinal);
        var exceptions = new List<string>();

        if (!string.IsNullOrWhiteSpace(order.PoNumber))
        {
            var chain = await _correlationRepository.GetCorrelationChainAsync(order.PoNumber, cancellationToken);
            var partnerDocs = chain
                .SelectMany(c => new[] { c.SourceDocument, c.TargetDocument })
                .Where(d => d is not null)
                .Select(d => d!)
                .Where(d => d.TenantId is null || d.TenantId == tenantId)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var d in partnerDocs)
            {
                var type = MapDocType(d.DocumentType);
                if (type is not null &&
                    (!byType.TryGetValue(type, out var existing) || d.ReceivedAt < existing.ReceivedAt))
                {
                    byType[type] = new OrderDocumentDto
                    {
                        Type = type,
                        ReceivedAt = d.ReceivedAt,
                        Id = d.Id,
                        Reference = d.ExternalReference,
                        ViewUrl = null
                    };
                }

                if (d.Status is DocumentStatus.Failed or DocumentStatus.FailedPermanent or DocumentStatus.ValidationFailed)
                {
                    var reason = d.ErrorDetails ?? d.LastErrorCode ?? "processing failed";
                    exceptions.Add($"{MapDocType(d.DocumentType) ?? d.DocumentType.ToString()}: {reason}");
                }
            }
        }

        // Back-fill PO/ACK/ASN from the order's own lifecycle when no correlated document exists yet.
        AddIfMissing(byType, "PO", order.SubmittedAt ?? order.OrderDate, order.EdiDocumentId, order.PoNumber);

        if (order.AcknowledgedAt.HasValue || order.AcknowledgmentDocumentId.HasValue ||
            order.Status is OrderStatus.Acknowledged or OrderStatus.Processing
                or OrderStatus.PartiallyShipped or OrderStatus.Shipped
                or OrderStatus.Delivered or OrderStatus.Completed)
        {
            AddIfMissing(byType, "ACK", order.AcknowledgedAt ?? order.OrderDate,
                order.AcknowledgmentDocumentId, order.PartnerOrderNumber);
        }

        if (order.ShippedAt.HasValue ||
            order.Status is OrderStatus.PartiallyShipped or OrderStatus.Shipped
                or OrderStatus.Delivered or OrderStatus.Completed)
        {
            AddIfMissing(byType, "ASN", order.ShippedAt ?? order.OrderDate, null, null);
        }

        if (order.Status == OrderStatus.Failed && !string.IsNullOrWhiteSpace(order.ErrorMessage))
            exceptions.Insert(0, order.ErrorMessage!);

        var documents = byType.Values.OrderBy(d => StageRank(d.Type)).ToList();
        return (documents, exceptions);
    }

    private static void AddIfMissing(Dictionary<string, OrderDocumentDto> byType, string type, DateTime? receivedAt, int? id, string? reference)
    {
        if (byType.ContainsKey(type))
            return;
        byType[type] = new OrderDocumentDto { Type = type, ReceivedAt = receivedAt, Id = id, Reference = reference, ViewUrl = null };
    }

    private static string? MapDocType(DocumentType type) => type switch
    {
        DocumentType.PurchaseOrder => "PO",
        DocumentType.PurchaseOrderAcknowledgment => "ACK",
        DocumentType.AdvanceShipNotice => "ASN",
        DocumentType.Invoice => "Invoice",
        _ => null
    };

    /// <summary>Clamps paging to the documented bounds: skip ≥ 0, take in [1, 200] (default 50).</summary>
    private static (int Skip, int Take) NormalizePaging(int skip, int take)
    {
        if (skip < 0) skip = 0;
        if (take <= 0) take = 50;
        if (take > 200) take = 200;
        return (skip, take);
    }

    /// <summary>
    /// Resolves a tenant, association-gating it to the org AND to the calling user's tenant scope.
    /// Returns 404 (no enumeration) when the tenant isn't the org's, or when a scoped portal user
    /// (TenantManager/Viewer without AllTenants) asks for a tenant outside their assigned set. The
    /// org-key/integration path has no per-user scope (<see cref="_resolvedUser"/> is null) → full
    /// org access, unchanged. Every <c>/tenants/{id}/…</c> endpoint routes through here (directly or
    /// via <see cref="ResolveTenantPartnerAsync"/>), so this is the single tenant-scope choke point.
    /// </summary>
    private async Task<(Tenant? Tenant, IActionResult? Error)> ResolveTenantAsync(Organization org, int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || tenant.OrganizationId != org.Id)
            return (null, NotFound(new { error = $"Tenant '{tenantId}' not found" }));

        // Per-user tenant scope (only when authenticated as a scoped portal user).
        if (_resolvedUser is { AllTenants: false } user && !user.ScopedTenantIds.Contains(tenantId))
            return (null, NotFound(new { error = $"Tenant '{tenantId}' not found" }));

        return (tenant, null);
    }

    /// <summary>
    /// Association-gates the tenant, then resolves the target partner connection: the tenant's
    /// approved connection matching <paramref name="partnerCode"/> (or its first approved connection
    /// when the code is omitted). Returns (null, null) when the tenant has no approved connection.
    /// </summary>
    private async Task<(TenantPartnerAccount? Connection, IActionResult? Error)> ResolveTenantPartnerAsync(
        Organization org, int tenantId, string? partnerCode, CancellationToken cancellationToken)
    {
        var (tenant, tenantError) = await ResolveTenantAsync(org, tenantId, cancellationToken);
        if (tenant is null)
            return (null, tenantError);

        var partners = await GetConnectedPartnersAsync(org.Id, tenantId, cancellationToken);

        var connection = string.IsNullOrWhiteSpace(partnerCode)
            ? partners.FirstOrDefault()
            : partners.FirstOrDefault(c => string.Equals(c.TradingPartner!.Code, partnerCode, StringComparison.OrdinalIgnoreCase));

        return (connection, null);
    }

    /// <summary>The tenant's approved connections (with TradingPartner loaded), one per partner.</summary>
    private async Task<List<TenantPartnerAccount>> GetConnectedPartnersAsync(int organizationId, int tenantId, CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetConnectionsAsync(
            organizationId, ConnectionApprovalStatus.Approved, cancellationToken);

        return connections
            .Where(c => c.TenantId == tenantId && c.TradingPartner is not null)
            .GroupBy(c => c.TradingPartnerId)
            .Select(g => g.First())
            .OrderBy(c => c.TradingPartner!.Name)
            .ToList();
    }

    /// <summary>
    /// The authenticated org-portal user for the current request, populated by
    /// <see cref="ResolveOrgAsync"/> when the caller presented a user token (null for the org-key path).
    /// </summary>
    private OrgUserContext? _resolvedUser;

    /// <summary>
    /// Resolves the calling organization. Two credentials are accepted:
    /// <list type="bullet">
    /// <item>a customer-portal <b>user token</b> (JWT bearer) — the org is taken from the token's
    /// <c>org_id</c> claim and the user's role + tenant scope are captured in <see cref="_resolvedUser"/>;</item>
    /// <item>the org <b>API key</b> (<c>X-Api-Key</c>) — machine/integration access (unchanged).</item>
    /// </list>
    /// Returns a 401 result when neither resolves to an active organization.
    /// </summary>
    private async Task<(Organization? Org, IActionResult? Error)> ResolveOrgAsync(CancellationToken cancellationToken)
    {
        // Path 1: an authenticated org-portal user token.
        if (string.Equals(User?.FindFirst(OrgUserTokenService.TokenTypeClaim)?.Value,
                OrgUserTokenService.OrgPortalUserTokenType, StringComparison.Ordinal))
        {
            var orgIdClaim = User!.FindFirst(ApiPrincipalExtensions.OrgIdClaim)?.Value;
            if (int.TryParse(orgIdClaim, out var orgId))
            {
                var orgFromUser = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
                if (orgFromUser is not null && orgFromUser.Status == OrganizationStatus.Active)
                {
                    _resolvedUser = BuildOrgUserContext();
                    return (orgFromUser, null);
                }
            }
            return (null, Unauthorized(new { error = "Invalid or inactive user" }));
        }

        // Path 2: the org API key (machine/integration).
        var apiKey = Request.Headers[ApiKeyHeader].FirstOrDefault();
        var org = await _authenticator.ResolveActiveOrganizationAsync(apiKey, cancellationToken);
        if (org is null)
            return (null, Unauthorized(new { error = "Invalid or missing API key" }));

        return (org, null);
    }

    /// <summary>
    /// Resolves the org AND the authenticated user's role + accessible tenant ids. When the caller
    /// used the org API key (no user), the returned context is null. Use this where per-user role or
    /// tenant scope matters; <see cref="ResolveOrgAsync"/> remains the org-only entry point.
    /// </summary>
    private async Task<(Organization? Org, OrgUserContext? User, IActionResult? Error)> ResolveOrgUserAsync(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return (null, null, error);
        return (org, _resolvedUser, null);
    }

    /// <summary>Reads the org-portal-user context from the validated JWT claims.</summary>
    private OrgUserContext BuildOrgUserContext()
    {
        Guid? userId = Guid.TryParse(User!.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var g)
            ? g
            : null;
        var email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        var displayName = User.FindFirst("name")?.Value;
        var role = Enum.TryParse<OrgPortalRole>(User.FindFirst(OrgUserTokenService.RoleClaim)?.Value, ignoreCase: true, out var r)
            ? r
            : OrgPortalRole.Viewer;

        var scopeRaw = User.FindFirst(OrgUserTokenService.TenantScopeClaim)?.Value;
        var allTenants = string.Equals(scopeRaw, OrgUserTokenService.TenantScopeAll, StringComparison.OrdinalIgnoreCase);
        var scopedTenantIds = allTenants || string.IsNullOrWhiteSpace(scopeRaw)
            ? Array.Empty<int>()
            : scopeRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();

        return new OrgUserContext(userId, displayName, email, role, allTenants, scopedTenantIds);
    }

    /// <summary>
    /// Portal capabilities surfaced in <c>/org/me</c> for a role — the UI gates nav + action buttons
    /// off these. They mirror the server-side RBAC: OrgAdmin gets the org-admin capabilities
    /// (<c>settings.write</c>, <c>users.manage</c>, <c>org.admin</c>) that TenantManager/Viewer never
    /// see, and Viewer gets read-only.
    /// </summary>
    private static List<string> CapabilitiesForRole(OrgPortalRole role) => role switch
    {
        OrgPortalRole.OrgAdmin => new List<string>
        {
            "connections.read", "connections.write", "orders.read", "users.manage", "settings.write", "org.admin"
        },
        OrgPortalRole.TenantManager => new List<string>
        {
            "connections.read", "connections.write", "orders.read"
        },
        _ => new List<string> { "connections.read", "orders.read" }
    };

    /// <summary>The authenticated org-portal user's identity + role + tenant scope (from the token).</summary>
    private sealed record OrgUserContext(
        Guid? UserId,
        string? DisplayName,
        string? Email,
        OrgPortalRole Role,
        bool AllTenants,
        IReadOnlyCollection<int> ScopedTenantIds);

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
    /// <summary>The user's id (Guid) when authenticated via a user token; null for the org-key path.</summary>
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
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

/// <summary>A generic paged list envelope: <c>{ items, total, skip, take }</c>.</summary>
public class PagedResult<T>
{
    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int total, int skip, int take)
    {
        Items = items;
        Total = total;
        Skip = skip;
        Take = take;
    }

    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }

    /// <summary>True when the rows are partner-level shared data (not per-tenant), e.g. inventory.</summary>
    public bool PartnerLevel { get; set; }

    public static PagedResult<T> Empty(int skip, int take) => new(new List<T>(), 0, skip, take);
}

/// <summary>A partner the tenant is connected to — drives the catalog partner selector.</summary>
public class OrgCatalogPartnerDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>A current-price row for a tenant + partner.</summary>
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

/// <summary>Price history for a SKU (empty <see cref="Points"/> if no history is available).</summary>
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

/// <summary>Stock-by-DC for a SKU. Inventory is partner-level shared data (see PagedResult.PartnerLevel).</summary>
public class InventoryRowDto
{
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Uom { get; set; }
    public DateTime? AsOf { get; set; }
    public List<InventoryDcDto> ByDistributionCenter { get; set; } = new();
}

public class InventoryDcDto
{
    public string Dc { get; set; } = string.Empty;
    public string? DcName { get; set; }
    public int OnHand { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Content coverage + subscription state for a tenant + partner (view-only).</summary>
public class ContentSummaryDto
{
    public int TotalSkus { get; set; }
    public int WithContent { get; set; }
    public decimal CoveragePct { get; set; }
    public bool Subscribed { get; set; }
    public DateTime? LastSyncAt { get; set; }
}

/// <summary>Per-SKU content availability for a tenant's catalog.</summary>
public class ContentRowDto
{
    public string Sku { get; set; } = string.Empty;
    public bool HasContent { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
}

/// <summary>A row in the tenant's Orders list.</summary>
public class OrderSummaryDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>The tenant (dealer) that placed the order — populated on the org-level combined view.</summary>
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;

    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }

    /// <summary>Order status (Draft | Submitted | Acknowledged | … | Cancelled | Failed).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Document-chain stages present, e.g. ["PO","ACK"]. Ordered PO → ACK → ASN → Invoice.</summary>
    public List<string> Chain { get; set; } = new();

    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>Full order detail: summary + lines + assembled document chain + exceptions.</summary>
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

    /// <summary>Plain-language exceptions from failed order/document processing (empty when healthy).</summary>
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

    /// <summary>Underlying document id (PartnerDocument/EDI), when known.</summary>
    public int? Id { get; set; }

    /// <summary>Business reference (PO number, partner order number, etc.), when known.</summary>
    public string? Reference { get; set; }

    /// <summary>View/download URL when available; null exposes metadata only.</summary>
    public string? ViewUrl { get; set; }
}

/// <summary>A single entry in the tenant activity feed.</summary>
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

/// <summary>A tenant row for the Organization → Tenants screen (read-only; operator-provisioned).</summary>
public class OrgTenantRowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>The M360 mapping (org-side / external id), when the tenant has been provisioned.</summary>
    public string? ExternalId { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public int ConnectionCount { get; set; }
    public int ActiveConnectionCount { get; set; }
}

/// <summary>The organization's editable profile — <c>GET/PUT /api/v1/org/settings</c>. Never carries secrets.</summary>
public class OrgSettingsDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Read-only lifecycle status (Pending | Active | Suspended | …).</summary>
    public string Status { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";

    /// <summary>False today — notification preferences aren't modeled; the portal shows a note.</summary>
    public bool NotificationPreferencesSupported { get; set; }
}

/// <summary>Body of <c>PUT /api/v1/org/settings</c>. Null fields are left unchanged; only real fields.</summary>
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

/// <summary>One-call dashboard summary for a tenant — <c>GET /api/v1/org/tenants/{id}/summary</c>.</summary>
public class TenantSummaryDto
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;

    /// <summary>Connection health, one row per partner the tenant has a connection with.</summary>
    public List<TenantConnectionHealthDto> Connections { get; set; } = new();

    public DateTime? LastPriceSyncAt { get; set; }
    public DateTime? LastContentSyncAt { get; set; }

    /// <summary>Failed feeds + failed orders + unresolved quarantined documents.</summary>
    public int OpenErrorCount { get; set; }

    /// <summary>The five most recent orders (newest first).</summary>
    public List<OrderSummaryDto> RecentOrders { get; set; } = new();
}

public class TenantConnectionHealthDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>Portal-facing status: Pending | Active | Suspended | Disconnected | Cancelled | Denied.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? LastSyncAt { get; set; }
}
