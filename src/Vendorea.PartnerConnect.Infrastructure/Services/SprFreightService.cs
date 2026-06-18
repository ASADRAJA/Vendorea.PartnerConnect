using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <inheritdoc />
public class SprFreightService : ISprFreightService
{
    private readonly SprWebServiceContextResolver _context;
    private readonly ISprInteractiveServices _spr;

    public SprFreightService(SprWebServiceContextResolver context, ISprInteractiveServices spr)
    {
        _context = context;
        _spr = spr;
    }

    public Task<FreightOutcome> FindRatesAsync(int organizationId, FreightRateRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(organizationId, request, lowestOnly: false, cancellationToken);

    public Task<FreightOutcome> LowestRateAsync(int organizationId, FreightRateRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(organizationId, request, lowestOnly: true, cancellationToken);

    private async Task<FreightOutcome> ExecuteAsync(int organizationId, FreightRateRequest request, bool lowestOnly, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return new FreightOutcome(FreightStatus.InvalidRequest, Error: validationError);

        var ctx = await _context.ResolveAsync(organizationId, request.ExternalTenantId, cancellationToken);
        switch (ctx.Status)
        {
            case SprContextStatus.NotConfigured:
                return new FreightOutcome(FreightStatus.NotConfigured, Error: "SPR web services are not configured for this partner");
            case SprContextStatus.NoActiveConnection:
                return new FreightOutcome(FreightStatus.NoActiveConnection);
        }

        var query = new SprFreightQuery
        {
            ShipFromDc = request.ShipFromDc,
            State = request.DestinationState,
            PostalCode = request.DestinationZip,
            Weight = request.TotalWeight,
            Carrier = request.Carrier,
            ServiceLevel = request.ServiceLevel,
            Residential = request.Residential
        };

        var result = lowestOnly
            ? await _spr.LowestFreightRateAsync(ctx.Config!, query, cancellationToken)
            : await _spr.FindFreightRatesAsync(ctx.Config!, query, cancellationToken);

        return new FreightOutcome(FreightStatus.Ok, Map(result));
    }

    private static string? Validate(FreightRateRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ExternalTenantId)) return "ExternalTenantId is required";
        if (r.ShipFromDc <= 0) return "ShipFromDc is required";
        if (string.IsNullOrWhiteSpace(r.DestinationState)) return "DestinationState is required";
        if (string.IsNullOrWhiteSpace(r.DestinationZip)) return "DestinationZip is required";
        if (r.TotalWeight <= 0) return "TotalWeight must be greater than 0";
        return null;
    }

    private static FreightRateResponse Map(SprFreightResult r) => new()
    {
        Success = r.Success,
        Message = r.Success ? (r.RtnMessage ?? "OK") : (r.ErrorMessage ?? r.RtnMessage),
        Rates = r.Rates.Select(x => new FreightRateOption
        {
            ShipFromDc = x.ShipFromDc,
            Carrier = x.Carrier,
            CarrierDescription = x.CarrierDescription,
            ShipVia = x.ShipVia,
            Rate = x.Rate,
            DeliveryDays = x.DeliveryDays,
            NumberOfCartons = x.NumberOfCartons,
            ServiceLevel = x.ServiceLevel,
            Residential = x.Residential
        }).ToList()
    };
}
