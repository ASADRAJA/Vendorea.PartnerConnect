using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
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
[AllowAnonymous] // Auth is performed per-request via the X-Api-Key header (see ResolveOrgAsync).
public class OrgController : ControllerBase
{
    private const string ApiKeyHeader = "X-Api-Key";

    private readonly IOrgApiKeyAuthenticator _authenticator;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantConnectionService _connectionService;
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly ILogger<OrgController> _logger;

    public OrgController(
        IOrgApiKeyAuthenticator authenticator,
        IOrganizationRepository organizationRepository,
        ITenantConnectionService connectionService,
        ITenantPartnerAccountRepository connectionRepository,
        ILogger<OrgController> logger)
    {
        _authenticator = authenticator;
        _organizationRepository = organizationRepository;
        _connectionService = connectionService;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    /// <summary>Lists the trading partners this org may connect to, with each partner's required fields.</summary>
    [HttpGet("partners")]
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

    /// <summary>Requests a tenant-partner connection (status Pending; tenant created on approval).</summary>
    [HttpPost("connections")]
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
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null)
            return error!;

        var connections = await _connectionRepository.GetConnectionsAsync(org.Id, null, cancellationToken);
        return Ok(connections.Select(MapConnection).ToList());
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
