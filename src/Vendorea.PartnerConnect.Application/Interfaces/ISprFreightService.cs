using Vendorea.PartnerConnect.Contracts.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Live SPR freight-rate lookups for an M360 dealer. Gated to dealers with an active SPR connection
/// (same rule as stock check). "FindRates" returns all qualifying rates; "LowestRate" returns the
/// single cheapest.
/// </summary>
public interface ISprFreightService
{
    Task<FreightOutcome> FindRatesAsync(int organizationId, FreightRateRequest request, CancellationToken cancellationToken = default);
    Task<FreightOutcome> LowestRateAsync(int organizationId, FreightRateRequest request, CancellationToken cancellationToken = default);
}

public enum FreightStatus { Ok, NoActiveConnection, NotConfigured, InvalidRequest }

public record FreightOutcome(FreightStatus Status, FreightRateResponse? Response = null, string? Error = null);
