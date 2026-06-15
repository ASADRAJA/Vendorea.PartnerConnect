using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for tenant-partner connections. A connection enrolls a tenant with a partner;
/// the tenant is created/linked only when the connection is approved.
/// </summary>
[ApiController]
[Route("api/admin/connections")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminConnectionsController : ControllerBase
{
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantConnectionService _connectionService;
    private readonly ILogger<AdminConnectionsController> _logger;

    public AdminConnectionsController(
        ITenantPartnerAccountRepository connectionRepository,
        IOrganizationRepository organizationRepository,
        ITradingPartnerRepository partnerRepository,
        ITenantRepository tenantRepository,
        ITenantConnectionService connectionService,
        ILogger<AdminConnectionsController> logger)
    {
        _connectionRepository = connectionRepository;
        _organizationRepository = organizationRepository;
        _partnerRepository = partnerRepository;
        _tenantRepository = tenantRepository;
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>Lists connections, optionally filtered by organization and approval status.</summary>
    [HttpGet]
    public async Task<IActionResult> GetConnections(
        [FromQuery] int? organizationId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        ConnectionApprovalStatus? statusFilter =
            Enum.TryParse<ConnectionApprovalStatus>(status, ignoreCase: true, out var s) ? s : null;

        var connections = await _connectionRepository.GetConnectionsAsync(organizationId, statusFilter, cancellationToken);
        var items = connections.Select(MapToDto).ToList();

        return Ok(new ConnectionListResult
        {
            Total = items.Count,
            PendingCount = items.Count(c => c.ApprovalStatus == "Pending"),
            ApprovedCount = items.Count(c => c.ApprovalStatus == "Approved"),
            DeniedCount = items.Count(c => c.ApprovalStatus == "Denied"),
            Items = items
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetConnection(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return connection is null ? NotFound() : Ok(MapToDto(connection));
    }

    /// <summary>Returns the partners an organization may connect to (selected at registration).</summary>
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] int organizationId, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdWithPartnersAsync(organizationId, cancellationToken);
        if (org is null)
            return NotFound();

        var partners = org.Partners
            .Where(p => p.TradingPartner != null)
            .Select(p => new ConnectionPartnerOptionDto
            {
                TradingPartnerId = p.TradingPartnerId,
                Name = p.TradingPartner!.Name,
                Code = p.TradingPartner.Code
            })
            .ToList();

        return Ok(partners);
    }

    /// <summary>Returns the partner's tenant-confirmation field names (drives the dynamic form).</summary>
    [HttpGet("partner-fields")]
    public async Task<IActionResult> GetPartnerFields([FromQuery] int tradingPartnerId, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (partner is null)
            return NotFound();

        return Ok(ParseConfirmationFieldNames(partner.TenantConfirmationFieldsJson));
    }

    /// <summary>Requests a new tenant-partner connection (status = Pending; no tenant yet).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateConnection([FromBody] CreateConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _connectionService.RequestConnectionAsync(
            request.OrganizationId,
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

        var detail = await _connectionRepository.GetByIdWithDetailsAsync(result.Connection!.Id, cancellationToken);
        return CreatedAtAction(nameof(GetConnection), new { id = result.Connection.Id }, MapToDto(detail!));
    }

    /// <summary>Approves a connection: creates the tenant if new (else links) and activates.</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveConnection(int id, [FromBody] DecisionRequest? request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (connection is null)
            return NotFound();
        if (connection.ApprovalStatus != ConnectionApprovalStatus.Pending)
            return BadRequest(new { error = "Only pending connections can be approved" });
        if (connection.OrganizationId is null)
            return BadRequest(new { error = "Connection has no organization" });

        // Guard the effective-status invariant: an active connection requires an active org.
        var org = await _organizationRepository.GetByIdAsync(connection.OrganizationId.Value, cancellationToken);
        if (org is null)
            return BadRequest(new { error = "Organization not found" });
        if (!EffectiveStatus.IsOrganizationActive(org))
            return BadRequest(new { error = "Cannot approve a connection while its organization is not active" });

        // Create the tenant if (org, external id) is new; otherwise link to the existing one.
        var tenant = await _tenantRepository.GetByOrgAndExternalIdAsync(
            connection.OrganizationId.Value, connection.ExternalTenantId, cancellationToken);

        // An active connection requires an active tenant — don't attach one to a suspended tenant.
        if (tenant is not null && tenant.Status != TenantStatus.Active)
            return BadRequest(new { error = "Cannot approve a connection for a tenant that is not active; reactivate the tenant first" });

        if (tenant is null)
        {
            tenant = new Tenant
            {
                OrganizationId = connection.OrganizationId.Value,
                ExternalId = connection.ExternalTenantId,
                Code = Truncate(connection.ExternalTenantId, 50),
                Name = BuildTenantName(connection),
                ContactFirstName = connection.ContactFirstName,
                ContactLastName = connection.ContactLastName,
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            tenant = await _tenantRepository.AddAsync(tenant, cancellationToken);
            _logger.LogInformation("Created tenant {TenantId} (org {OrgId}, external {ExternalId}) on connection approval",
                tenant.Id, tenant.OrganizationId, tenant.ExternalId);
        }
        else if (string.IsNullOrEmpty(tenant.ContactFirstName) && !string.IsNullOrEmpty(connection.ContactFirstName))
        {
            tenant.ContactFirstName = connection.ContactFirstName;
            tenant.ContactLastName = connection.ContactLastName;
            await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        connection.TenantId = tenant.Id;
        connection.ApprovalStatus = ConnectionApprovalStatus.Approved;
        connection.IsActive = true;
        connection.VerifiedAt = DateTime.UtcNow;
        connection.DecidedAt = DateTime.UtcNow;
        connection.DecisionReason = request?.Reason;
        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Approved connection {ConnectionId} -> tenant {TenantId}", id, tenant.Id);
        return NoContent();
    }

    /// <summary>Denies a pending connection.</summary>
    [HttpPost("{id:int}/deny")]
    public async Task<IActionResult> DenyConnection(int id, [FromBody] DecisionRequest? request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);
        if (connection is null)
            return NotFound();

        connection.ApprovalStatus = ConnectionApprovalStatus.Denied;
        connection.IsActive = false;
        connection.DecidedAt = DateTime.UtcNow;
        connection.DecisionReason = request?.Reason;
        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Denied connection {ConnectionId}", id);
        return NoContent();
    }

    private static string BuildTenantName(TenantPartnerAccount c)
    {
        var name = $"{c.ContactFirstName} {c.ContactLastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? c.ExternalTenantId : name;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static List<string> ParseConfirmationFieldNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static ConnectionDto MapToDto(TenantPartnerAccount c)
    {
        Dictionary<string, string>? fields = null;
        if (!string.IsNullOrWhiteSpace(c.ConfirmationFieldsJson))
        {
            try { fields = JsonSerializer.Deserialize<Dictionary<string, string>>(c.ConfirmationFieldsJson); }
            catch { /* ignore */ }
        }

        return new ConnectionDto
        {
            Id = c.Id,
            OrganizationId = c.OrganizationId ?? 0,
            OrganizationName = c.Organization?.Name,
            TradingPartnerId = c.TradingPartnerId,
            PartnerName = c.TradingPartner?.Name,
            ExternalTenantId = c.ExternalTenantId,
            TenantId = c.TenantId,
            TenantName = c.Tenant?.Name,
            AccountNumber = c.AccountNumber,
            ContactFirstName = c.ContactFirstName,
            ContactLastName = c.ContactLastName,
            SpecialIdentifyingCode = c.SpecialIdentifyingCode,
            Notes = c.Notes,
            ConfirmationFields = fields ?? new Dictionary<string, string>(),
            ApprovalStatus = c.ApprovalStatus.ToString(),
            DecisionReason = c.DecisionReason,
            CreatedAt = c.CreatedAt,
            DecidedAt = c.DecidedAt
        };
    }
}

public class ConnectionDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? SpecialIdentifyingCode { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string> ConfirmationFields { get; set; } = new();
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ConnectionListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public List<ConnectionDto> Items { get; set; } = new();
}

public class ConnectionPartnerOptionDto
{
    public int TradingPartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CreateConnectionRequest
{
    public int OrganizationId { get; set; }
    public int TradingPartnerId { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? SpecialIdentifyingCode { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string>? ConfirmationFields { get; set; }
}

public class DecisionRequest
{
    public string? Reason { get; set; }
}
