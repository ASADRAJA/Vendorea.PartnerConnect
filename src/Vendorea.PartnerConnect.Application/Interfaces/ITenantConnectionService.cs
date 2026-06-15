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

/// <summary>
/// Encapsulates the rules for requesting a tenant-partner connection so the admin and org-facing
/// entry points enforce the same validation (org active, partner whitelisted, no duplicate).
/// </summary>
public interface ITenantConnectionService
{
    Task<RequestConnectionResult> RequestConnectionAsync(
        int organizationId, RequestConnectionInput input, CancellationToken cancellationToken = default);
}
