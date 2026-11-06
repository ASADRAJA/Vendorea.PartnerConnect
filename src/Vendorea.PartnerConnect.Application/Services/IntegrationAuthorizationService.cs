using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationAuthorizationService : IIntegrationAuthorizationService
{
    private readonly IIntegrationOAuthStateService _states;
    private readonly IIntegrationConnectionService _connections;
    private readonly IIntegrationProviderRepository _providers;
    private readonly IAmazonLwaClient _lwa;
    private readonly IAmazonAccessTokenCache _tokenCache;
    private readonly IntegrationAuthorizationOptions _options;
    private readonly ILogger<IntegrationAuthorizationService> _logger;

    public IntegrationAuthorizationService(
        IIntegrationOAuthStateService states,
        IIntegrationConnectionService connections,
        IIntegrationProviderRepository providers,
        IAmazonLwaClient lwa,
        IAmazonAccessTokenCache tokenCache,
        IOptions<IntegrationAuthorizationOptions> options,
        ILogger<IntegrationAuthorizationService> logger)
    {
        _states = states;
        _connections = connections;
        _providers = providers;
        _lwa = lwa;
        _tokenCache = tokenCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IntegrationAuthorizationStart> StartAsync(
        int organizationId, int tenantId, string providerCode, string? displayName,
        string? returnUrl, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetByCodeAsync(providerCode, cancellationToken);
        if (provider is null || !provider.IsEnabled)
        {
            return IntegrationAuthorizationStart.Failed($"Provider '{providerCode}' is not available.");
        }

        if (provider.AuthKind != IntegrationAuthKind.OAuth2)
        {
            return IntegrationAuthorizationStart.Failed(
                $"Provider '{provider.Code}' does not authorize through OAuth.");
        }

        if (!_lwa.IsConfigured)
        {
            // Said plainly, and before a connection row exists. The alternative is a seller
            // staring at an Amazon error page while the problem is in our configuration.
            _logger.LogError(
                "Authorization for {ProviderCode} was requested but Amazon:Lwa is not configured.",
                provider.Code);
            return IntegrationAuthorizationStart.Failed(
                "Amazon authorization is not configured on this environment.");
        }

        // Checked before anything is created. A URL that fails the allow-list is refused here
        // rather than stored and rejected at redirect time, so a state can never carry a
        // destination we would decline to honour.
        if (!string.IsNullOrWhiteSpace(returnUrl) && !_options.IsReturnUrlAllowed(returnUrl))
        {
            _logger.LogWarning(
                "Rejected a return URL outside the allow-list while starting authorization for tenant {TenantId}.",
                tenantId);
            return IntegrationAuthorizationStart.Failed("returnUrl is not an allowed destination.");
        }

        // A fresh Pending row every time. Which connection this becomes is decided only after
        // Amazon names the selling partner — see CompleteAuthorizationAsync.
        var begun = await _connections.BeginAsync(
            organizationId, tenantId, provider.Code, displayName, cancellationToken);

        if (!begun.Success)
        {
            return IntegrationAuthorizationStart.Failed(begun.Error ?? "Could not start authorization.");
        }

        var state = await _states.IssueAsync(
            organizationId, tenantId, begun.Connection!.Id, returnUrl, cancellationToken);

        _logger.LogInformation(
            "Issued authorization state for {ProviderCode} connection {ConnectionId}, tenant {TenantId}",
            provider.Code, begun.Connection.Id, tenantId);

        return IntegrationAuthorizationStart.Ok(
            begun.Connection.Id,
            _lwa.BuildConsentUrl(state),
            DateTime.UtcNow.Add(IIntegrationOAuthStateService.Lifetime));
    }

    public async Task<IntegrationAuthorizationCompletion> CompleteAsync(
        string providerCode, string? state, string? authorizationCode, string? externalAccountId,
        CancellationToken cancellationToken = default)
    {
        // The state is validated and consumed before anything else happens. An exchange attempted
        // first would let an attacker burn authorization codes and read Amazon's responses through
        // our timing, without ever holding a valid state.
        var oauthState = await _states.ValidateAndConsumeAsync(state ?? string.Empty, cancellationToken);
        if (oauthState is null)
        {
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.InvalidState,
                "The authorization link is invalid or has expired.");
        }

        if (string.IsNullOrWhiteSpace(authorizationCode) || string.IsNullOrWhiteSpace(externalAccountId))
        {
            _logger.LogWarning(
                "Authorization callback for connection {ConnectionId} arrived without a code or account id.",
                oauthState.IntegrationConnectionId);
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.MissingCode,
                "The provider did not return an authorization code.");
        }

        // Tenant comes from the state, never from the request. This single line is what prevents
        // a crafted callback from attaching an Amazon account to another tenant's connection:
        // the lookup is scoped to the tenant that started the flow, so a connection belonging to
        // anyone else simply is not found.
        var connection = await _connections.GetAsync(
            oauthState.TenantId, oauthState.IntegrationConnectionId, cancellationToken);

        if (connection is null)
        {
            _logger.LogWarning(
                "Authorization state referenced connection {ConnectionId}, which tenant {TenantId} does not own.",
                oauthState.IntegrationConnectionId, oauthState.TenantId);
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.ConnectionUnavailable,
                "The connection is no longer available.");
        }

        var provider = await _providers.GetByIdAsync(connection.IntegrationProviderId, cancellationToken);
        if (provider is null || !provider.IsEnabled)
        {
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.ConnectionUnavailable,
                "The provider is no longer available.");
        }

        // The route's provider must be the connection's. Providers have different callback
        // semantics and different credentials; a state issued for one must not be redeemed
        // through another's door, even though both end up here.
        if (!string.Equals(provider.Code, providerCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "State for connection {ConnectionId} ({Expected}) was presented on the {Actual} callback.",
                connection.Id, provider.Code, providerCode);
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.InvalidState,
                "The authorization link is invalid or has expired.");
        }

        var token = await _lwa.ExchangeAuthorizationCodeAsync(authorizationCode, cancellationToken);
        if (!token.Success || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            // token.Error is Amazon's error code, not its response body — see AmazonLwaClient.
            _logger.LogWarning(
                "Token exchange failed for connection {ConnectionId}: {Error}",
                connection.Id, token.Error);
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.ProviderRejected,
                "Authorization could not be completed with the provider.");
        }

        // Granted capabilities are the provider's, because SP-API grants roles to the application
        // and reports nothing per seller in this response. When a call later fails for a missing
        // role, that is recorded against the connection rather than guessed at here.
        var result = await _connections.CompleteAuthorizationAsync(
            oauthState.TenantId,
            oauthState.IntegrationConnectionId,
            externalAccountId.Trim(),
            token.RefreshToken!,
            provider.Capabilities,
            region: connection.Region,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return IntegrationAuthorizationCompletion.Failed(
                IntegrationAuthorizationFailure.ConnectionUnavailable, result.Error ?? "Failed.");
        }

        // The refresh token has changed, so anything cached against this connection is stale.
        // Reusing a reauthorised connection makes this necessary rather than merely tidy: the id
        // is the same, so a stale entry would otherwise still look valid.
        _tokenCache.Invalidate(result.Connection!.Id);

        _logger.LogInformation(
            "Completed authorization for connection {ConnectionId} (tenant {TenantId}); reused existing: {Reused}",
            result.Connection.Id, oauthState.TenantId, result.ReusedExisting);

        return IntegrationAuthorizationCompletion.Ok(
            result.Connection.Id, oauthState.ReturnUrl, result.ReusedExisting);
    }
}
