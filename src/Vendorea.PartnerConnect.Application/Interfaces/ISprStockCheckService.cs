using Vendorea.PartnerConnect.Contracts.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Orchestrates an M360 dealer's live SPR stock/price check: resolves the dealer's active SPR
/// connection (for the SPR customer number + pricing) and the partner's web-service credentials,
/// gates access to SPR-subscribed tenants only, and calls the appropriate SPR service.
/// </summary>
public interface ISprStockCheckService
{
    Task<StockCheckOutcome> StockCheckAsync(int organizationId, StockCheckRequest request, CancellationToken cancellationToken = default);
}

public enum StockCheckStatus
{
    /// <summary>Call was made to SPR (inspect <see cref="StockCheckResponse.Success"/> for the partner result).</summary>
    Ok,
    /// <summary>The tenant has no effectively-active SPR connection — not allowed.</summary>
    NoActiveConnection,
    /// <summary>SPR web services aren't configured on the partner (missing base URL / credentials).</summary>
    NotConfigured,
    /// <summary>Bad input (missing item number / external tenant id).</summary>
    InvalidRequest
}

public record StockCheckOutcome(StockCheckStatus Status, StockCheckResponse? Response = null, string? Error = null);
