using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationAccountDelegationService : IIntegrationAccountDelegationService
{
    private readonly IIntegrationAccountDelegationRepository _delegations;
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationConnectionService _connectionService;
    private readonly IIntegrationProviderRepository _providers;
    private readonly IAuditService _audit;
    private readonly ILogger<IntegrationAccountDelegationService> _logger;

    public IntegrationAccountDelegationService(
        IIntegrationAccountDelegationRepository delegations,
        IIntegrationConnectionRepository connections,
        IIntegrationConnectionService connectionService,
        IIntegrationProviderRepository providers,
        IAuditService audit,
        ILogger<IntegrationAccountDelegationService> logger)
    {
        _delegations = delegations;
        _connections = connections;
        _connectionService = connectionService;
        _providers = providers;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IntegrationDelegationResult> GrantAsync(
        string providerCode, string externalAccountId, int delegateTenantId,
        string grantedBy, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(grantedBy))
        {
            // Not a formality. A delegated-access decision nobody is named for is an unexplained
            // exception sitting in a table.
            return IntegrationDelegationResult.Failed("grantedBy is required.");
        }

        var provider = await _providers.GetByCodeAsync(providerCode, cancellationToken);
        if (provider is null)
        {
            return IntegrationDelegationResult.Failed($"Provider '{providerCode}' is not known.");
        }

        var ownerTenantId = await _connections.FindOwningTenantAsync(
            provider.Id, externalAccountId, cancellationToken);

        if (ownerTenantId is null)
        {
            // Nothing to delegate. The tenant should connect the account itself and become its
            // owner; pre-authorising a delegation would create an orphan grant that silently
            // became meaningful the moment somebody else claimed the account.
            return IntegrationDelegationResult.Failed(
                "No tenant currently owns this account, so there is nothing to delegate. " +
                "Connect it from the owning tenant first.");
        }

        if (ownerTenantId == delegateTenantId)
        {
            return IntegrationDelegationResult.Failed(
                "That tenant already owns this account.");
        }

        var existing = await _delegations.FindActiveAsync(
            provider.Id, externalAccountId, delegateTenantId, cancellationToken);

        if (existing is not null)
        {
            // Idempotent: asking twice is not an error, and a second row would make "is this
            // delegated?" ambiguous.
            return IntegrationDelegationResult.Ok(existing);
        }

        var delegation = new IntegrationAccountDelegation
        {
            IntegrationProviderId = provider.Id,
            ExternalAccountId = externalAccountId,
            OwnerTenantId = ownerTenantId.Value,
            DelegateTenantId = delegateTenantId,
            GrantedBy = grantedBy,
            Reason = reason
        };

        await _delegations.AddAsync(delegation, cancellationToken);

        _logger.LogWarning(
            "Delegated access to {ProviderCode} account {Account} granted to tenant {DelegateTenantId} "
            + "by {GrantedBy} (owner: tenant {OwnerTenantId}).",
            provider.Code, externalAccountId, delegateTenantId, grantedBy, ownerTenantId);

        // Audited against both tenants: the owner has a right to know their account was shared,
        // and the delegate's auditors need to see why they hold it.
        await _audit.LogAsync(
            AuditAction.Create, "IntegrationAccountDelegation", delegation.Id.ToString(),
            newValues: new
            {
                provider.Code,
                ExternalAccountId = externalAccountId,
                OwnerTenantId = ownerTenantId.Value,
                DelegateTenantId = delegateTenantId,
                GrantedBy = grantedBy,
                Reason = reason
            },
            dealerId: ownerTenantId.Value,
            notes: $"Delegated access granted to tenant {delegateTenantId}.",
            cancellationToken: cancellationToken);

        return IntegrationDelegationResult.Ok(delegation);
    }

    public async Task<IntegrationDelegationResult> RevokeAsync(
        Guid delegationId, string revokedBy, CancellationToken cancellationToken = default)
    {
        var delegation = await _delegations.GetAsync(delegationId, cancellationToken);
        if (delegation is null)
        {
            return IntegrationDelegationResult.Failed("Delegation not found.");
        }

        if (!delegation.IsActive)
        {
            return IntegrationDelegationResult.Ok(delegation);
        }

        delegation.RevokedAt = DateTime.UtcNow;
        delegation.RevokedBy = revokedBy;
        await _delegations.SaveAsync(cancellationToken);

        // The grant is withdrawn; now make it true. The delegate's connection is disconnected,
        // which destroys its credentials — otherwise revocation would be a record of a decision
        // rather than the decision taking effect.
        var connection = await _connections.FindByExternalAccountAsync(
            delegation.DelegateTenantId, delegation.IntegrationProviderId,
            delegation.ExternalAccountId, cancellationToken);

        if (connection is not null && connection.Status != IntegrationConnectionStatus.Revoked)
        {
            await _connectionService.DisconnectAsync(
                delegation.DelegateTenantId, connection.Id, cancellationToken);
        }

        _logger.LogWarning(
            "Delegated access to account {Account} withdrawn from tenant {DelegateTenantId} by {RevokedBy}.",
            delegation.ExternalAccountId, delegation.DelegateTenantId, revokedBy);

        await _audit.LogAsync(
            AuditAction.Update, "IntegrationAccountDelegation", delegation.Id.ToString(),
            newValues: new
            {
                RevokedBy = revokedBy,
                delegation.DelegateTenantId,
                DisconnectedConnectionId = connection?.Id
            },
            dealerId: delegation.OwnerTenantId,
            notes: $"Delegated access withdrawn from tenant {delegation.DelegateTenantId}.",
            cancellationToken: cancellationToken);

        return IntegrationDelegationResult.Ok(delegation);
    }

    public async Task<IReadOnlyList<IntegrationAccountDelegation>> ListForAccountAsync(
        string providerCode, string externalAccountId, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetByCodeAsync(providerCode, cancellationToken);
        return provider is null
            ? Array.Empty<IntegrationAccountDelegation>()
            : await _delegations.GetForAccountAsync(provider.Id, externalAccountId, cancellationToken);
    }
}
