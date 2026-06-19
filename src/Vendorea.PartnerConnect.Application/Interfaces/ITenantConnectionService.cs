using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Input for requesting a new tenant-partner connection. Shared by the admin portal flow
/// (PC staff create on a tenant's behalf) and the org-facing API flow (tenant initiates).
/// </summary>
public record RequestConnectionInput(
    int TradingPartnerId,
    string ExternalTenantId,
    string AccountNumber,
    string? ContactFirstName = null,
    string? ContactLastName = null,
    string? SpecialIdentifyingCode = null,
    string? Notes = null,
    Dictionary<string, string>? ConfirmationFields = null);

/// <summary>Outcome of a connection request.</summary>
public record RequestConnectionResult(bool Success, string? Error, TenantPartnerAccount? Connection)
{
    public static RequestConnectionResult Failed(string error) => new(false, error, null);
    public static RequestConnectionResult Succeeded(TenantPartnerAccount connection) => new(true, null, connection);
}

/// <summary>Result of a cancel/unsubscribe state change, mapping to HTTP outcomes.</summary>
public enum ConnectionChangeStatus
{
    /// <summary>The change was applied (or was already in the requested state — idempotent).</summary>
    Ok,
    /// <summary>No connection exists for the supplied (organization, tenant, partner).</summary>
    NotFound,
    /// <summary>A connection exists but its current state doesn't allow the requested change.</summary>
    InvalidState
}

/// <summary>Outcome of a cancel/unsubscribe operation.</summary>
public record ConnectionChangeResult(ConnectionChangeStatus Status, string? Error, TenantPartnerAccount? Connection)
{
    public static ConnectionChangeResult Ok(TenantPartnerAccount connection) => new(ConnectionChangeStatus.Ok, null, connection);
    public static ConnectionChangeResult NotFound(string error) => new(ConnectionChangeStatus.NotFound, error, null);
    public static ConnectionChangeResult Invalid(string error) => new(ConnectionChangeStatus.InvalidState, error, null);
}

/// <summary>
/// Encapsulates the rules for the tenant-partner connection lifecycle so the admin and org-facing
/// entry points enforce the same validation (org active, partner whitelisted, no duplicate).
/// </summary>
public interface ITenantConnectionService
{
    Task<RequestConnectionResult> RequestConnectionAsync(
        int organizationId, RequestConnectionInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a still-<c>Pending</c> connection request on the merchant's behalf. The connection is
    /// identified by the org (from the API key), the merchant's tenant id (stored as ExternalTenantId)
    /// and PartnerConnect's trading-partner id. Idempotent if already cancelled.
    /// </summary>
    Task<ConnectionChangeResult> CancelConnectionAsync(
        int organizationId, string externalTenantId, int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes (disables) an <c>Approved</c> connection on the merchant's behalf — orders and
    /// live web services are gated off immediately. Idempotent if already unsubscribed.
    /// </summary>
    Task<ConnectionChangeResult> UnsubscribeConnectionAsync(
        int organizationId, string externalTenantId, int tradingPartnerId, CancellationToken cancellationToken = default);
}
