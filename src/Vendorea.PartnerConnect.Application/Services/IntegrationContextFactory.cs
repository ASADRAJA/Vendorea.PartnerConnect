using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationContextFactory : IIntegrationContextFactory
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationProviderRepository _providers;
    private readonly IIntegrationCredentialStore _credentials;

    public IntegrationContextFactory(
        IIntegrationConnectionRepository connections,
        IIntegrationProviderRepository providers,
        IIntegrationCredentialStore credentials)
    {
        _connections = connections;
        _providers = providers;
        _credentials = credentials;
    }

    // Correlation is passed in rather than resolved here: ICorrelationContext lives in
    // Infrastructure, which sits above Application, and inverting that for one string would be a
    // worse trade than letting the caller supply the id it already holds.
    public async Task<IntegrationContext> CreateAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
        => await CreateAsync(tenantId, connectionId, correlationId: null, cancellationToken);

    public async Task<IntegrationContext> CreateAsync(
        int tenantId, Guid connectionId, string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // Scoped by tenant at the repository, so a context for another tenant's connection cannot
        // be built even by passing the right id.
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Connection {connectionId} was not found for tenant {tenantId}.");

        if (connection.Status != IntegrationConnectionStatus.Connected)
        {
            // Fail here rather than let an adapter discover mid-request that its credentials were
            // revoked. The status is the authoritative answer to "may we still call this provider".
            throw new InvalidOperationException(
                $"Connection {connectionId} is {connection.Status} and cannot be used.");
        }

        var provider = connection.Provider
            ?? await _providers.GetByIdAsync(connection.IntegrationProviderId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Provider {connection.IntegrationProviderId} is missing for connection {connectionId}.");

        var refreshToken = await _credentials.GetAsync(
            connectionId, IntegrationCredentialType.RefreshToken, cancellationToken);

        return new IntegrationContext(
            connection,
            provider,
            refreshToken,
            correlationId ?? Guid.NewGuid().ToString());
    }
}
