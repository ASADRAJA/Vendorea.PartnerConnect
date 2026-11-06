using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class AmazonAccessTokenProvider : IAmazonAccessTokenProvider
{
    /// <summary>
    /// LWA error codes that mean the refresh token is finished, not merely unlucky.
    /// </summary>
    private static readonly string[] TerminalErrors =
    {
        "invalid_grant",     // the seller revoked consent, or the token was replaced
        "invalid_client",    // application credentials wrong — not the seller's fault, but no retry helps
        "unauthorized_client"
    };

    private readonly IIntegrationConnectionService _connections;
    private readonly IIntegrationCredentialStore _credentials;
    private readonly IAmazonLwaClient _lwa;
    private readonly IAmazonAccessTokenCache _cache;
    private readonly ILogger<AmazonAccessTokenProvider> _logger;

    public AmazonAccessTokenProvider(
        IIntegrationConnectionService connections,
        IIntegrationCredentialStore credentials,
        IAmazonLwaClient lwa,
        IAmazonAccessTokenCache cache,
        ILogger<AmazonAccessTokenProvider> logger)
    {
        _connections = connections;
        _credentials = credentials;
        _lwa = lwa;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        // Tenant-scoped, like every other connection read. An adapter holding only a connection id
        // still cannot reach another tenant's token.
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            // Expired, Revoked and Suspended all mean "do not call Amazon". Checking here rather
            // than in each adapter is what makes revocation actually stop work.
            _logger.LogDebug(
                "Connection {ConnectionId} is {Status}; no access token issued.",
                connectionId, connection.Status);
            return null;
        }

        return await _cache.GetOrRefreshAsync(connectionId, ct => RefreshAsync(tenantId, connectionId, ct),
            cancellationToken);
    }

    private async Task<LwaTokenResult> RefreshAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken)
    {
        var refreshToken = await _credentials.GetAsync(
            connectionId, IntegrationCredentialType.RefreshToken, cancellationToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            await _connections.RecordCredentialRejectedAsync(
                tenantId, connectionId, "No refresh token is stored for this connection.",
                cancellationToken);
            return LwaTokenResult.Failed("No refresh token stored.");
        }

        var result = await _lwa.RefreshAccessTokenAsync(refreshToken, cancellationToken);
        if (result.Success)
        {
            return result;
        }

        // The distinction the merchant feels: "reconnect your Amazon account" versus nothing at
        // all. Treating every failure as terminal would disconnect healthy sellers during an
        // Amazon outage; treating none as terminal would leave revoked connections polling for ever.
        if (IsTerminal(result.Error))
        {
            await _connections.RecordCredentialRejectedAsync(
                tenantId, connectionId, result.Error ?? "The provider rejected the refresh token.",
                cancellationToken);
        }
        else
        {
            await _connections.RecordTransientErrorAsync(
                tenantId, connectionId, result.Error ?? "Token refresh failed.", cancellationToken);
        }

        return result;
    }

    private static bool IsTerminal(string? error) =>
        error is not null
        && TerminalErrors.Any(e => error.Contains(e, StringComparison.OrdinalIgnoreCase));
}
