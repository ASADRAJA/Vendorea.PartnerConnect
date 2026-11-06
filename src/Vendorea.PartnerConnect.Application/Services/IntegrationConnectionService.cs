using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationConnectionService : IIntegrationConnectionService
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationProviderRepository _providers;
    private readonly IIntegrationCredentialStore _credentials;
    private readonly IAuditService _audit;
    private readonly ILogger<IntegrationConnectionService> _logger;

    public IntegrationConnectionService(
        IIntegrationConnectionRepository connections,
        IIntegrationProviderRepository providers,
        IIntegrationCredentialStore credentials,
        IAuditService audit,
        ILogger<IntegrationConnectionService> logger)
    {
        _connections = connections;
        _providers = providers;
        _credentials = credentials;
        _audit = audit;
        _logger = logger;
    }

    public Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        int tenantId, CancellationToken cancellationToken = default)
        => _connections.GetForTenantAsync(tenantId, cancellationToken);

    public Task<IntegrationConnection?> GetAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
        => _connections.GetAsync(tenantId, connectionId, cancellationToken);

    public async Task<IntegrationConnectionResult> BeginAsync(
        int organizationId, int tenantId, string providerCode, string? displayName,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetByCodeAsync(providerCode, cancellationToken);
        if (provider is null || !provider.IsEnabled)
        {
            return IntegrationConnectionResult.Failed($"Provider '{providerCode}' is not available.");
        }

        // Pending, with no external account id: we do not know which account the user will
        // authorise until the provider tells us. The unique index is filtered precisely so that
        // several pending rows can coexist — a user who abandons the flow and retries is normal.
        var connection = await _connections.AddAsync(new IntegrationConnection
        {
            OrganizationId = organizationId,
            TenantId = tenantId,
            IntegrationProviderId = provider.Id,
            DisplayName = displayName,
            Status = IntegrationConnectionStatus.Pending
        }, cancellationToken);

        await _audit.LogAsync(
            AuditAction.Create, "IntegrationConnection", connection.Id.ToString(),
            newValues: new { provider.Code, tenantId, Status = nameof(IntegrationConnectionStatus.Pending) },
            dealerId: tenantId,
            notes: "Authorization started.",
            cancellationToken: cancellationToken);

        return IntegrationConnectionResult.Ok(connection);
    }

    public async Task<IntegrationConnectionResult> CompleteAuthorizationAsync(
        int tenantId, Guid pendingConnectionId, string externalAccountId,
        string refreshToken, IntegrationCapabilities grantedCapabilities,
        string? region = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var pending = await _connections.GetAsync(tenantId, pendingConnectionId, cancellationToken);
        if (pending is null)
        {
            return IntegrationConnectionResult.Failed("Connection not found.");
        }

        // Identity is the external account, not this row. If the tenant already has a connection
        // for this account — Connected, Expired or Revoked — reauthorise THAT one and throw the
        // pending row away. Creating a second would start with no watermark, re-import every
        // order as new, and break the one-Amazon-order-to-one-M360-order guarantee that the
        // (connection, externalOrderId) index exists to provide.
        var existing = await _connections.FindByExternalAccountAsync(
            tenantId, pending.IntegrationProviderId, externalAccountId, cancellationToken);

        var reused = existing is not null && existing.Id != pending.Id;
        var target = existing ?? pending;

        var previousStatus = target.Status;

        target.ExternalAccountId = externalAccountId;
        target.Region = region ?? target.Region;
        target.GrantedCapabilities = grantedCapabilities;
        target.Status = IntegrationConnectionStatus.Connected;
        target.LastError = null;
        target.LastErrorAt = null;

        if (reused && !string.IsNullOrWhiteSpace(pending.DisplayName))
        {
            // Carry across the name the user just typed, but keep everything else — the id, the
            // watermarks, the order history.
            target.DisplayName = pending.DisplayName;
        }

        await _connections.UpdateAsync(target, cancellationToken);

        // Written after the connection is Connected: a credential attached to a row that failed
        // to update would be a secret with no owner.
        await _credentials.PutAsync(
            target.Id, IntegrationCredentialType.RefreshToken, refreshToken,
            expiresAt: null, cancellationToken);

        if (reused)
        {
            // The abandoned pending row would otherwise sit in the list for ever.
            pending.Status = IntegrationConnectionStatus.Revoked;
            pending.LastError = "Superseded by reauthorization of the same account.";
            pending.LastErrorAt = DateTime.UtcNow;
            await _connections.UpdateAsync(pending, cancellationToken);

            _logger.LogInformation(
                "Reauthorized existing connection {ConnectionId} for account {Account}; discarded pending {PendingId}",
                target.Id, externalAccountId, pending.Id);
        }

        await _audit.LogAsync(
            AuditAction.Update, "IntegrationConnection", target.Id.ToString(),
            newValues: new
            {
                Status = nameof(IntegrationConnectionStatus.Connected),
                PreviousStatus = previousStatus.ToString(),
                ExternalAccountId = externalAccountId,
                GrantedCapabilities = grantedCapabilities.ToString(),
                Reused = reused
            },
            dealerId: tenantId,
            notes: reused ? "Reauthorized an existing connection." : "Authorization completed.",
            cancellationToken: cancellationToken);

        return IntegrationConnectionResult.Ok(target, reused);
    }

    public async Task<IntegrationConnectionResult> DisconnectAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null)
        {
            return IntegrationConnectionResult.Failed("Connection not found.");
        }

        // Credentials go first. If the status update failed afterwards we would rather have a
        // Connected row with no usable token than a Revoked row whose token still works.
        await _credentials.RevokeAllAsync(connectionId, cancellationToken);

        connection.Status = IntegrationConnectionStatus.Revoked;
        await _connections.UpdateAsync(connection, cancellationToken);

        // The row stays: it carries the audit trail, and reconnecting the same account later must
        // find it rather than start fresh.
        await _audit.LogAsync(
            AuditAction.Update, "IntegrationConnection", connectionId.ToString(),
            newValues: new { Status = nameof(IntegrationConnectionStatus.Revoked) },
            dealerId: tenantId,
            notes: "Disconnected; credentials destroyed.",
            cancellationToken: cancellationToken);

        return IntegrationConnectionResult.Ok(connection);
    }
}
