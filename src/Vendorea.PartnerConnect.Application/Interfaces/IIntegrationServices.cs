using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Reads and writes connection secrets, encrypting on the way in and decrypting on the way out.
/// </summary>
/// <remarks>
/// The only component that handles plaintext credentials. Callers receive a decrypted value at
/// the moment of use and are expected not to retain it; nothing here caches one.
/// </remarks>
public interface IIntegrationCredentialStore
{
    /// <summary>Stores or replaces a secret, encrypted. Bumps the version on replacement.</summary>
    Task PutAsync(Guid connectionId, IntegrationCredentialType type, string plaintext,
        DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the decrypted secret, or null when none is stored.
    /// </summary>
    /// <remarks>
    /// Uses the strict decryption path: a stored value that is not ciphertext can only mean
    /// encryption did not happen, and must throw rather than be used as though it had.
    /// </remarks>
    Task<string?> GetAsync(Guid connectionId, IntegrationCredentialType type,
        CancellationToken cancellationToken = default);

    /// <summary>Destroys every secret for a connection. Used on disconnect and revocation.</summary>
    Task RevokeAllAsync(Guid connectionId, CancellationToken cancellationToken = default);
}

/// <summary>Everything an adapter needs for one call, scoped to one connection.</summary>
/// <remarks>
/// Built per call and never cached. A client cached by region or provider rather than by
/// connection would serve one tenant's credentials to another — the failure mode most easily
/// introduced later by a well-meaning optimisation.
/// </remarks>
public sealed record IntegrationContext(
    IntegrationConnection Connection,
    IntegrationProvider Provider,
    string? RefreshToken,
    string CorrelationId)
{
    public int TenantId => Connection.TenantId;
    public Guid ConnectionId => Connection.Id;

    /// <summary>True when the provider actually granted this capability to this connection.</summary>
    public bool Can(IntegrationCapabilities capability) =>
        Connection.GrantedCapabilities.HasFlag(capability);
}

public interface IIntegrationContextFactory
{
    /// <summary>
    /// Builds a context for one call. Throws when the connection is not usable, rather than
    /// returning something an adapter would discover was unusable mid-request.
    /// </summary>
    Task<IntegrationContext> CreateAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>As above, carrying the caller's correlation id through to the provider call.</summary>
    Task<IntegrationContext> CreateAsync(
        int tenantId, Guid connectionId, string? correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Connection lifecycle: create, authorise, reauthorise, disconnect.</summary>
public interface IIntegrationConnectionService
{
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<IntegrationConnection?> GetAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>Creates a Pending connection with no external account id and no credentials.</summary>
    Task<IntegrationConnectionResult> BeginAsync(
        int organizationId, int tenantId, string providerCode, string? displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes authorisation once the provider has told us which account was authorised.
    /// </summary>
    /// <remarks>
    /// Reuses this tenant's existing connection for that account when one exists, in any state
    /// including Revoked, and discards the pending row. That is what keeps the connection id,
    /// the sync watermarks and the order associations stable across a reconnect.
    /// </remarks>
    Task<IntegrationConnectionResult> CompleteAuthorizationAsync(
        int tenantId, Guid pendingConnectionId, string externalAccountId,
        string refreshToken, IntegrationCapabilities grantedCapabilities,
        string? region = null, CancellationToken cancellationToken = default);

    /// <summary>Revokes a connection and destroys its credentials, retaining the row for audit.</summary>
    Task<IntegrationConnectionResult> DisconnectAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the provider rejected this connection's credentials, moving it to Expired.
    /// </summary>
    /// <remarks>
    /// Distinct from a transient failure on purpose. A rejected refresh token will keep being
    /// rejected, so the connection must stop looking healthy and start telling the merchant to
    /// reconnect — a poller that retries it forever simply burns the rate-limit budget of the
    /// connections that do work.
    ///
    /// The credential row is left in place: it is already useless, and deleting it would destroy
    /// the version history that shows when re-consent last happened.
    /// </remarks>
    Task RecordCredentialRejectedAsync(
        int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failure that may well succeed next time, without changing the status.
    /// </summary>
    Task RecordTransientErrorAsync(
        int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a capability the provider turned out not to have granted.
    /// </summary>
    /// <remarks>
    /// SP-API reports nothing per seller at authorization time, so a missing role surfaces only
    /// as a 403 on the first real call. Recording it here is what stops every later poll from
    /// rediscovering the same denial, and what lets the portal say which capability is missing
    /// instead of showing a connection that is green but does nothing.
    /// </remarks>
    Task RecordCapabilityDeniedAsync(
        int tenantId, Guid connectionId, IntegrationCapabilities capability, string error,
        CancellationToken cancellationToken = default);
}

public sealed record IntegrationConnectionResult(
    bool Success,
    IntegrationConnection? Connection,
    string? Error,
    bool ReusedExisting = false)
{
    public static IntegrationConnectionResult Ok(IntegrationConnection c, bool reused = false)
        => new(true, c, null, reused);

    public static IntegrationConnectionResult Failed(string error)
        => new(false, null, error);
}

/// <summary>
/// Grants and withdraws delegated access to a seller account one tenant owns.
/// </summary>
/// <remarks>
/// Separate from <see cref="IIntegrationConnectionService"/> on purpose. Connecting an account is
/// something a dealer does for themselves; delegating one is something an administrator does on
/// two dealers' behalf, and the two must not share an entry point. Vendorea provides software: it
/// does not become a party to a dealer's Amazon account, and a delegation records a decision
/// somebody made rather than one the platform took.
/// </remarks>
public interface IIntegrationAccountDelegationService
{
    /// <summary>
    /// Grants a second tenant the right to connect an account another tenant owns.
    /// </summary>
    /// <remarks>
    /// Refuses when nobody owns the account yet — there is nothing to delegate, and the tenant
    /// should simply connect it — and when the delegate is the owner.
    /// </remarks>
    Task<IntegrationDelegationResult> GrantAsync(
        string providerCode, string externalAccountId, int delegateTenantId,
        string grantedBy, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a grant and disconnects the delegate.
    /// </summary>
    /// <remarks>
    /// Disconnecting is the point. A revocation that left the delegate's credentials working
    /// would be a record of a decision rather than the decision taking effect.
    /// </remarks>
    Task<IntegrationDelegationResult> RevokeAsync(
        Guid delegationId, string revokedBy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationAccountDelegation>> ListForAccountAsync(
        string providerCode, string externalAccountId, CancellationToken cancellationToken = default);
}

public sealed record IntegrationDelegationResult(
    bool Success, IntegrationAccountDelegation? Delegation, string? Error)
{
    public static IntegrationDelegationResult Ok(IntegrationAccountDelegation delegation)
        => new(true, delegation, null);

    public static IntegrationDelegationResult Failed(string error) => new(false, null, error);
}
