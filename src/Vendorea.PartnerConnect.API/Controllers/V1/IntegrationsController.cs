using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Tenant-scoped integration connections (Amazon and any future provider).
/// </summary>
/// <remarks>
/// Every route resolves through the same org + tenant checks the rest of the org-facing API uses,
/// and returns 404 rather than 403 for a tenant outside the caller's scope, so tenant existence
/// cannot be probed.
///
/// Phase 1 is deliberately the non-OAuth surface: list, detail, begin, disconnect. The authorize
/// and callback endpoints are Phase 2.
/// </remarks>
[ApiController]
[Route("api/v1/org")]
[Produces("application/json")]
[Authorize]
public class IntegrationsController : ControllerBase
{
    private readonly IIntegrationConnectionService _connections;
    private readonly IIntegrationAuthorizationService _authorization;
    private readonly IIntegrationProviderRepository _providers;
    private readonly IIntegrationCredentialRepository _credentials;
    private readonly IIntegrationSyncStateRepository _syncStates;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgApiKeyAuthenticator _authenticator;
    private readonly ILogger<IntegrationsController> _logger;

    public IntegrationsController(
        IIntegrationConnectionService connections,
        IIntegrationAuthorizationService authorization,
        IIntegrationProviderRepository providers,
        IIntegrationCredentialRepository credentials,
        IIntegrationSyncStateRepository syncStates,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgApiKeyAuthenticator authenticator,
        ILogger<IntegrationsController> logger)
    {
        _connections = connections;
        _authorization = authorization;
        _providers = providers;
        _credentials = credentials;
        _syncStates = syncStates;
        _organizations = organizations;
        _tenants = tenants;
        _authenticator = authenticator;
        _logger = logger;
    }

    /// <summary>Lists the providers this organization may connect to.</summary>
    [HttpGet("integration-providers")]
    [RequireScope(ApiScopes.IntegrationsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationProviderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var providers = await _providers.GetEnabledAsync(cancellationToken);
        return Ok(providers.Select(MapProvider).ToList());
    }

    /// <summary>Lists a tenant's integration connections.</summary>
    [HttpGet("tenants/{tenantId:int}/integrations")]
    [RequireScope(ApiScopes.IntegrationsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConnections(int tenantId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        var connections = await _connections.ListAsync(tenantId, cancellationToken);
        return Ok(connections.Select(MapConnection).ToList());
    }

    /// <summary>Connection detail, including credential metadata and sync health.</summary>
    [HttpGet("tenants/{tenantId:int}/integrations/{connectionId:guid}")]
    [RequireScope(ApiScopes.IntegrationsRead)]
    [ProducesResponseType(typeof(IntegrationConnectionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConnection(
        int tenantId, Guid connectionId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null) return NotFoundConnection();

        // Metadata only: type, version, expiry. The protected value is never projected, so this
        // response cannot leak a secret even if a future field is added carelessly.
        var credentials = await _credentials.DescribeAsync(connectionId, cancellationToken);
        var syncStates = await _syncStates.GetForConnectionAsync(connectionId, cancellationToken);

        return Ok(new IntegrationConnectionDetailDto
        {
            Connection = MapConnection(connection),
            Credentials = credentials
                .Select(c => new IntegrationCredentialSummaryDto
                {
                    CredentialType = c.Type.ToString(),
                    Version = c.Version,
                    ExpiresAt = c.ExpiresAt
                })
                .ToList(),
            SyncStates = syncStates
                .Select(s => new IntegrationSyncStateDto
                {
                    Capability = s.Capability.ToString(),
                    MarketplaceId = s.MarketplaceId,
                    Watermark = s.Watermark,
                    LastSuccessAt = s.LastSuccessAt,
                    LastAttemptAt = s.LastAttemptAt,
                    ConsecutiveFailures = s.ConsecutiveFailures,
                    LastError = s.LastError
                })
                .ToList()
        });
    }

    /// <summary>Begins a connection: creates it Pending, with no account id and no credentials.</summary>
    /// <remarks>
    /// For OAuth providers the authorize route below is the one to call — it does this and issues
    /// the state in a single step, which is what the published contract describes. This route
    /// remains for providers whose credentials arrive by other means (API key, SFTP), where there
    /// is a connection to configure but no consent to obtain.
    /// </remarks>
    [HttpPost("tenants/{tenantId:int}/integrations")]
    [RequireScope(ApiScopes.IntegrationsWrite)]
    [ProducesResponseType(typeof(IntegrationConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BeginConnection(
        int tenantId, [FromBody] BeginIntegrationConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        if (request is null || string.IsNullOrWhiteSpace(request.ProviderCode))
            return UnprocessableEntity(new { code = "VALIDATION_FAILED", message = "providerCode is required." });

        var result = await _connections.BeginAsync(
            org.Id, tenantId, request.ProviderCode, request.DisplayName, cancellationToken);

        if (!result.Success)
            return UnprocessableEntity(new { code = "PROVIDER_UNAVAILABLE", message = result.Error });

        _logger.LogInformation(
            "Began {ProviderCode} connection {ConnectionId} for tenant {TenantId}",
            request.ProviderCode, result.Connection!.Id, tenantId);

        return CreatedAtAction(
            nameof(GetConnection),
            new { tenantId, connectionId = result.Connection.Id },
            MapConnection(result.Connection));
    }

    /// <summary>
    /// Begins provider authorization: creates a Pending connection, issues a single-use state,
    /// and returns the consent URL.
    /// </summary>
    /// <remarks>
    /// The caller sends the user's browser to <c>consentUrl</c>. It is not a redirect from here,
    /// because the caller is a server (Merchant360) holding an org API key, not the browser.
    ///
    /// Which connection this ultimately becomes is not settled yet. If the seller authorises an
    /// account this tenant already holds, the row created here is discarded and the existing one
    /// reauthorised — see the completion path.
    /// </remarks>
    [HttpPost("tenants/{tenantId:int}/integrations/{providerCode}/authorize")]
    [RequireScope(ApiScopes.IntegrationsWrite)]
    [ProducesResponseType(typeof(IntegrationAuthorizationStartDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartAuthorization(
        int tenantId, string providerCode, [FromBody] StartAuthorizationRequest? request,
        CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        var result = await _authorization.StartAsync(
            org.Id, tenantId, providerCode, request?.DisplayName, request?.ReturnUrl,
            cancellationToken);

        if (!result.Success)
        {
            return UnprocessableEntity(new
            {
                code = "AUTHORIZATION_UNAVAILABLE",
                message = result.Error
            });
        }

        _logger.LogInformation(
            "Started {ProviderCode} authorization for connection {ConnectionId}, tenant {TenantId}",
            providerCode, result.ConnectionId, tenantId);

        return CreatedAtAction(
            nameof(GetConnection),
            new { tenantId, connectionId = result.ConnectionId },
            new IntegrationAuthorizationStartDto
            {
                IntegrationConnectionId = result.ConnectionId!.Value,
                ConsentUrl = result.ConsentUrl!,
                StateExpiresAt = result.ExpiresAt!.Value
            });
    }

    /// <summary>Disconnects a connection and destroys its credentials. The row is retained.</summary>
    [HttpDelete("tenants/{tenantId:int}/integrations/{connectionId:guid}")]
    [RequireScope(ApiScopes.IntegrationsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disconnect(
        int tenantId, Guid connectionId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        var result = await _connections.DisconnectAsync(tenantId, connectionId, cancellationToken);
        return result.Success ? NoContent() : NotFoundConnection();
    }

    // ---- scoping helpers ----

    /// <summary>
    /// Resolves the calling organization from an org API key.
    /// </summary>
    /// <remarks>
    /// Phase 1 accepts the org key only. The per-user token path lives in OrgController and will
    /// be shared in Phase 2 rather than copied here — duplicating tenant-scope logic is exactly
    /// how a second, subtly weaker check gets introduced.
    /// </remarks>
    private async Task<(Organization? Org, IActionResult? Error)> ResolveOrgAsync(
        CancellationToken cancellationToken)
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var org = await _authenticator.ResolveActiveOrganizationAsync(apiKey, cancellationToken);

        return org is null
            ? (null, Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid or missing API key" }))
            : (org, null);
    }

    /// <summary>
    /// 404, never 403, when the tenant is not the caller's — so tenant ids cannot be probed.
    /// </summary>
    private async Task<IActionResult?> EnsureTenantInScopeAsync(
        Organization org, int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        return tenant is null || tenant.OrganizationId != org.Id
            ? NotFound(new { code = "NOT_FOUND", message = $"Tenant '{tenantId}' not found" })
            : null;
    }

    private IActionResult NotFoundConnection() =>
        NotFound(new { code = "NOT_FOUND", message = "Connection not found" });

    // ---- mapping ----

    private static IntegrationProviderDto MapProvider(IntegrationProvider p) => new()
    {
        Code = p.Code,
        Name = p.Name,
        Kind = p.Kind.ToString(),
        AuthKind = p.AuthKind.ToString(),
        Capabilities = SplitCapabilities(p.Capabilities)
    };

    private static IntegrationConnectionDto MapConnection(IntegrationConnection c) => new()
    {
        Id = c.Id,
        ProviderCode = c.Provider?.Code ?? string.Empty,
        DisplayName = c.DisplayName,
        ExternalAccountId = c.ExternalAccountId,
        Region = c.Region,
        Status = c.Status.ToString(),
        GrantedCapabilities = SplitCapabilities(c.GrantedCapabilities),
        LastErrorAt = c.LastErrorAt,
        LastError = c.LastError,
        CreatedAt = c.CreatedAt
    };

    /// <summary>Flags enum to the contract's string array — no Amazon or CLR types on the wire.</summary>
    private static List<string> SplitCapabilities(IntegrationCapabilities capabilities) =>
        Enum.GetValues<IntegrationCapabilities>()
            .Where(v => v != IntegrationCapabilities.None && capabilities.HasFlag(v))
            .Select(v => v.ToString())
            .ToList();
}
