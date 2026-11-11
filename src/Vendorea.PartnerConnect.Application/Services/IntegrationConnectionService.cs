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
    private readonly IIntegrationAccountDelegationRepository _delegations;
    private readonly IAuditService _audit;
    private readonly ILogger<IntegrationConnectionService> _logger;

    public IntegrationConnectionService(
        IIntegrationConnectionRepository connections,
        IIntegrationProviderRepository providers,
        IIntegrationCredentialStore credentials,
        IIntegrationAccountDelegationRepository delegations,
        IAuditService audit,
        ILogger<IntegrationConnectionService> logger)
    {
        _connections = connections;
        _providers = providers;
        _credentials = credentials;
        _delegations = delegations;
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

        // A seller account maps to one Merchant360 tenant. Vendorea is a software provider, not a
        // merchant of record or an intermediary: a dealer's Amazon account, its orders, its
        // customers and its proceeds belong to that dealer's tenant. If another tenant already
        // owns this account, authorising it here is refused unless somebody has explicitly
        // delegated it — a browser redirect must never be able to move an account between
        // dealers, and silently allowing it would give two parties the same seller's orders.
        var ownership = await ResolveOwnershipAsync(
            tenantId, pending.IntegrationProviderId, externalAccountId, existing, cancellationToken);

        if (ownership is null)
        {
            _logger.LogWarning(
                "Tenant {TenantId} tried to authorize account {Account}, which another tenant owns and has not delegated.",
                tenantId, externalAccountId);

            await _audit.LogAsync(
                AuditAction.Update, "IntegrationConnection", pending.Id.ToString(),
                newValues: new { ExternalAccountId = externalAccountId, Refused = "AccountOwnedByAnotherTenant" },
                dealerId: tenantId,
                notes: "Authorization refused: the account belongs to another tenant and no delegation exists.",
                cancellationToken: cancellationToken);

            return IntegrationConnectionResult.Failed(
                "This account is already connected to a different Merchant360 tenant. " +
                "Delegated access must be granted before it can be connected here.");
        }

        var reused = existing is not null && existing.Id != pending.Id;
        var target = existing ?? pending;
        target.AccountOwnership = ownership.Value;

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
                AccountOwnership = ownership.Value.ToString(),
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

    /// <summary>
    /// Decides whether this tenant may hold the account, and on what footing.
    /// </summary>
    /// <remarks>
    /// Returns null when it may not. Three outcomes: the tenant already holds the account, so
    /// nothing changes; nobody holds it, so this tenant becomes its owner; or another tenant
    /// holds it, in which case only an active delegation makes this permissible.
    ///
    /// The cross-tenant lookup returns a tenant id and nothing else — no connection, no
    /// credentials, no orders. Only the existence of a claim crosses the boundary.
    /// </remarks>
    private async Task<IntegrationAccountOwnership?> ResolveOwnershipAsync(
        int tenantId, int providerId, string externalAccountId,
        IntegrationConnection? existing, CancellationToken cancellationToken)
    {
        // Reauthorising what this tenant already holds. Keep whatever footing it was on: a
        // delegated connection must not quietly promote itself to owner by reconnecting.
        if (existing is not null)
        {
            return existing.AccountOwnership;
        }

        var owner = await _connections.FindOwningTenantAsync(providerId, externalAccountId, cancellationToken);

        if (owner is null || owner == tenantId)
        {
            return IntegrationAccountOwnership.Primary;
        }

        var delegation = await _delegations.FindActiveAsync(
            providerId, externalAccountId, tenantId, cancellationToken);

        if (delegation is null)
        {
            return null;
        }

        _logger.LogInformation(
            "Tenant {TenantId} is connecting account {Account} under delegation {DelegationId} from tenant {OwnerTenantId}.",
            tenantId, externalAccountId, delegation.Id, owner);

        return IntegrationAccountOwnership.Delegated;
    }

    public async Task RecordCredentialRejectedAsync(
        int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        // Revoked stays Revoked. A disconnected connection whose stale token is rejected must not
        // be quietly promoted to Expired, which reads as "reconnect me" rather than "I am off".
        if (connection.Status == IntegrationConnectionStatus.Revoked)
        {
            return;
        }

        var previousStatus = connection.Status;
        connection.Status = IntegrationConnectionStatus.Expired;
        connection.LastError = Truncate(error);
        connection.LastErrorAt = DateTime.UtcNow;
        await _connections.UpdateAsync(connection, cancellationToken);

        if (previousStatus != IntegrationConnectionStatus.Expired)
        {
            _logger.LogWarning(
                "Connection {ConnectionId} for tenant {TenantId} needs reauthorization: the provider rejected its credentials.",
                connectionId, tenantId);

            await _audit.LogAsync(
                AuditAction.Update, "IntegrationConnection", connectionId.ToString(),
                newValues: new
                {
                    Status = nameof(IntegrationConnectionStatus.Expired),
                    PreviousStatus = previousStatus.ToString()
                },
                dealerId: tenantId,
                notes: "Provider rejected the stored credentials.",
                cancellationToken: cancellationToken);
        }
    }

    public async Task RecordTransientErrorAsync(
        int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        // Status deliberately untouched. A network blip is not a reason to tell a merchant their
        // Amazon account is disconnected; the error text and timestamp are enough for an operator.
        connection.LastError = Truncate(error);
        connection.LastErrorAt = DateTime.UtcNow;
        await _connections.UpdateAsync(connection, cancellationToken);
    }

    public async Task RecordCapabilityDeniedAsync(
        int tenantId, Guid connectionId, IntegrationCapabilities capability, string error,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null || !connection.GrantedCapabilities.HasFlag(capability))
        {
            return;
        }

        connection.GrantedCapabilities &= ~capability;
        connection.LastError = Truncate($"{capability} was denied by the provider: {error}");
        connection.LastErrorAt = DateTime.UtcNow;
        await _connections.UpdateAsync(connection, cancellationToken);

        _logger.LogWarning(
            "Connection {ConnectionId} lost capability {Capability}: the provider denied it.",
            connectionId, capability);

        await _audit.LogAsync(
            AuditAction.Update, "IntegrationConnection", connectionId.ToString(),
            newValues: new
            {
                DeniedCapability = capability.ToString(),
                GrantedCapabilities = connection.GrantedCapabilities.ToString()
            },
            dealerId: tenantId,
            notes: "Capability denied by the provider.",
            cancellationToken: cancellationToken);
    }

    /// <summary>LastError is bounded at 2000 characters; a provider body can be far longer.</summary>
    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
