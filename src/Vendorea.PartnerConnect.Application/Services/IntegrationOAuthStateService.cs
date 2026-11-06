using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationOAuthStateService : IIntegrationOAuthStateService
{
    private const int TokenBytes = 32; // 256 bits, same as the portal activation tokens

    private readonly IIntegrationOAuthStateRepository _states;
    private readonly ILogger<IntegrationOAuthStateService> _logger;

    public IntegrationOAuthStateService(
        IIntegrationOAuthStateRepository states,
        ILogger<IntegrationOAuthStateService> logger)
    {
        _states = states;
        _logger = logger;
    }

    public async Task<string> IssueAsync(
        int organizationId, int tenantId, Guid connectionId, string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var rawToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        await _states.AddAsync(new IntegrationOAuthState
        {
            TokenHash = HashToken(rawToken),
            OrganizationId = organizationId,
            TenantId = tenantId,
            IntegrationConnectionId = connectionId,
            ReturnUrl = returnUrl,
            ExpiresAt = DateTime.UtcNow.Add(IIntegrationOAuthStateService.Lifetime)
        }, cancellationToken);

        // The raw token is returned once and never stored. Not logged either — it is the
        // credential that authorises attaching an Amazon account to this tenant.
        return rawToken;
    }

    public async Task<IntegrationOAuthState?> ValidateAndConsumeAsync(
        string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var state = await _states.GetByHashAsync(HashToken(rawToken), cancellationToken);
        if (state is null)
        {
            // No detail in the log: an attacker probing states should learn nothing from timing
            // or from what we record.
            _logger.LogWarning("OAuth callback presented an unknown state token.");
            return null;
        }

        if (state.UsedAt.HasValue)
        {
            _logger.LogWarning(
                "OAuth state for connection {ConnectionId} was replayed.", state.IntegrationConnectionId);
            return null;
        }

        if (state.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "OAuth state for connection {ConnectionId} expired.", state.IntegrationConnectionId);
            return null;
        }

        // Consumed before the caller does anything with it, so a slow exchange cannot leave a
        // window for a second callback carrying the same state.
        await _states.MarkUsedAsync(state, cancellationToken);
        return state;
    }

    /// <summary>Lowercase-hex SHA-256 — the only form persisted.</summary>
    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    /// <summary>URL-safe, unpadded base64 so the token drops straight into a query string.</summary>
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
