using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IIntegrationOAuthStateRepository
{
    Task AddAsync(IntegrationOAuthState state, CancellationToken cancellationToken = default);
    Task<IntegrationOAuthState?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(IntegrationOAuthState state, CancellationToken cancellationToken = default);
    Task<int> PurgeExpiredAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}

/// <summary>Issues and validates the single-use state that binds a redirect to its tenant.</summary>
public interface IIntegrationOAuthStateService
{
    /// <summary>How long a state stays valid. Long enough to log in to Amazon, no longer.</summary>
    static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    Task<string> IssueAsync(
        int organizationId, int tenantId, Guid connectionId, string? returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and immediately consumes a state. Returns null for anything not currently valid —
    /// unknown, expired, or already used.
    /// </summary>
    /// <remarks>
    /// Deliberately one operation. Validating and consuming separately leaves a window in which two
    /// concurrent callbacks both see a valid state, which is precisely the replay this prevents.
    /// The caller cannot forget to consume because there is no way to validate without doing so.
    /// </remarks>
    Task<IntegrationOAuthState?> ValidateAndConsumeAsync(
        string rawToken, CancellationToken cancellationToken = default);
}

/// <summary>Exchanges Login with Amazon authorization codes and refreshes access tokens.</summary>
public interface IAmazonLwaClient
{
    /// <summary>
    /// Whether the application credentials are present.
    /// </summary>
    /// <remarks>
    /// Checked before a consent URL is built. Without it an unconfigured environment sends the
    /// seller to Amazon with an empty application id and the failure surfaces as an Amazon error
    /// page, which is the hardest place for an operator to diagnose it.
    /// </remarks>
    bool IsConfigured { get; }

    /// <summary>Exchanges a one-time authorization code for a long-lived refresh token.</summary>
    Task<LwaTokenResult> ExchangeAuthorizationCodeAsync(
        string authorizationCode, CancellationToken cancellationToken = default);

    /// <summary>Mints a short-lived access token from a refresh token.</summary>
    Task<LwaTokenResult> RefreshAccessTokenAsync(
        string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the consent URL the seller is sent to, carrying the state token.
    /// </summary>
    /// <remarks>
    /// The application id and the draft/published flag come from the client's own configuration
    /// rather than from the caller, so no layer above has to know what an SP-API application id
    /// is — or get the draft flag wrong.
    /// </remarks>
    string BuildConsentUrl(string state);
}

public sealed record LwaTokenResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAtUtc,
    string? Error)
{
    public static LwaTokenResult Ok(string? access, string? refresh, DateTime? expires)
        => new(true, access, refresh, expires, null);

    public static LwaTokenResult Failed(string error)
        => new(false, null, null, null, error);
}

/// <summary>
/// Short-lived access tokens, held in memory only.
/// </summary>
/// <remarks>
/// Never persisted: an access token lives about an hour, so storing it adds exposure for no
/// benefit — the refresh token can always mint another.
///
/// Keyed by connection id and nothing else. A cache keyed by region or provider would hand one
/// seller's token to another seller's request, and it is the kind of key that looks like a
/// reasonable optimisation right up until it leaks across tenants.
/// </remarks>
public interface IAmazonAccessTokenCache
{
    Task<string?> GetOrRefreshAsync(
        Guid connectionId, Func<CancellationToken, Task<LwaTokenResult>> refresh,
        CancellationToken cancellationToken = default);

    void Invalidate(Guid connectionId);
}

/// <summary>
/// Drives the two halves of an OAuth authorization: start, then callback.
/// </summary>
/// <remarks>
/// Deliberately the only thing a controller calls. The callback half decides the tenant from the
/// state token and from nothing else — no route value, no query parameter, no header. Putting
/// that decision here rather than in the controller means a second entry point (a portal
/// endpoint, a different provider) cannot reintroduce a caller-supplied tenant id.
/// </remarks>
public interface IIntegrationAuthorizationService
{
    /// <summary>
    /// Creates a pending connection, issues a state bound to it, and returns the consent URL.
    /// </summary>
    /// <remarks>
    /// One call rather than two. The connection and the state that authorises writing to it are
    /// created together, so there is no window in which a connection exists that any state could
    /// be pointed at.
    /// </remarks>
    Task<IntegrationAuthorizationStart> StartAsync(
        int organizationId, int tenantId, string providerCode, string? displayName,
        string? returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an authorization from the provider's redirect.
    /// </summary>
    /// <param name="providerCode">The provider whose callback route was used.</param>
    /// <param name="state">The opaque state the provider echoed back.</param>
    /// <param name="authorizationCode">The provider's one-time code.</param>
    /// <param name="externalAccountId">The account the provider says was authorised.</param>
    Task<IntegrationAuthorizationCompletion> CompleteAsync(
        string providerCode, string? state, string? authorizationCode, string? externalAccountId,
        CancellationToken cancellationToken = default);
}

public sealed record IntegrationAuthorizationStart(
    bool Success, Guid? ConnectionId, string? ConsentUrl, DateTime? ExpiresAt, string? Error)
{
    public static IntegrationAuthorizationStart Ok(Guid connectionId, string consentUrl, DateTime expiresAt)
        => new(true, connectionId, consentUrl, expiresAt, null);

    public static IntegrationAuthorizationStart Failed(string error)
        => new(false, null, null, null, error);
}

public sealed record IntegrationAuthorizationCompletion(
    bool Success,
    Guid? ConnectionId,
    string? ReturnUrl,
    bool ReusedExisting,
    IntegrationAuthorizationFailure Failure = IntegrationAuthorizationFailure.None,
    string? Error = null)
{
    public static IntegrationAuthorizationCompletion Ok(Guid connectionId, string? returnUrl, bool reused)
        => new(true, connectionId, returnUrl, reused);

    public static IntegrationAuthorizationCompletion Failed(
        IntegrationAuthorizationFailure failure, string error)
        => new(false, null, null, false, failure, error);
}

/// <summary>
/// Why a callback was rejected. Kept coarse on purpose: the browser is told only "this did not
/// work", and the detail stays in the log where it helps an operator rather than a prober.
/// </summary>
public enum IntegrationAuthorizationFailure
{
    None,
    InvalidState,
    MissingCode,
    ConnectionUnavailable,
    ProviderRejected
}

/// <summary>
/// Supplies a usable Amazon access token for one connection, or nothing.
/// </summary>
/// <remarks>
/// The single door adapters use. They never see a refresh token, never decide whether a
/// connection is healthy, and never cache anything themselves — which is what keeps the
/// credential-handling surface to the two classes behind this interface.
/// </remarks>
public interface IAmazonAccessTokenProvider
{
    /// <summary>
    /// Returns an access token, refreshing if needed. Null means the connection cannot be used
    /// right now; the reason has already been recorded against it.
    /// </summary>
    Task<string?> GetAccessTokenAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default);
}
