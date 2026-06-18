using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <inheritdoc />
public class SprStockCheckService : ISprStockCheckService
{
    private readonly SprWebServiceContextResolver _context;
    private readonly ISprInteractiveServices _spr;

    public SprStockCheckService(SprWebServiceContextResolver context, ISprInteractiveServices spr)
    {
        _context = context;
        _spr = spr;
    }

    public async Task<StockCheckOutcome> StockCheckAsync(int organizationId, StockCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ItemNumber) || string.IsNullOrWhiteSpace(request.ExternalTenantId))
            return new StockCheckOutcome(StockCheckStatus.InvalidRequest, Error: "ItemNumber and ExternalTenantId are required");

        var ctx = await _context.ResolveAsync(organizationId, request.ExternalTenantId, cancellationToken);
        switch (ctx.Status)
        {
            case SprContextStatus.NotConfigured:
                return new StockCheckOutcome(StockCheckStatus.NotConfigured, Error: "SPR web services are not configured for this partner");
            case SprContextStatus.NoActiveConnection:
                return new StockCheckOutcome(StockCheckStatus.NoActiveConnection);
        }

        var query = new SprStockCheckQuery
        {
            ItemNumber = request.ItemNumber,
            DcNumbers = request.DcNumbers ?? new List<int>(),
            AvailableOnly = request.AvailableOnly
        };

        // ≤8 specified DCs → Quick Check Plus; otherwise Dealer Stock Check (all DCs + price).
        var result = query.DcNumbers.Count is > 0 and <= 8
            ? await _spr.QuickCheckPlusAsync(ctx.Config!, query, cancellationToken)
            : await _spr.DealerStockCheckAsync(ctx.Config!, query, cancellationToken);

        return new StockCheckOutcome(StockCheckStatus.Ok, Map(result));
    }

    private static StockCheckResponse Map(SprStockCheckResult r) => new()
    {
        Success = r.Success,
        Message = r.Success ? (r.RtnMessage ?? "OK") : (r.ErrorMessage ?? r.RtnMessage),
        ItemNumber = r.SprItemNumber,
        Upc = r.Upc,
        Description = r.Description,
        ItemStatus = r.ItemStatus,
        UnitOfMeasure = r.SellUom,
        OrderMinimum = r.OrderMinimum,
        RetailPrice = r.RetailPrice,
        HazmatMessage = r.HazmatMessage,
        PricingIncluded = r.DealerPrice.HasValue,
        DealerPrice = r.DealerPrice,
        Discountable = r.Discountable,
        PriceDescription = r.PriceDescription,
        DistributionCenters = r.Dcs.Select(d => new DcAvailability
        {
            DcNumber = d.DcNumber,
            DcName = d.DcName,
            Available = d.Available,
            UnitOfMeasure = d.Uom,
            OnOrder = d.OnOrder,
            Expected = d.Expected,
            Sprinter = d.Sprinter,
            CutOff = d.CutOff,
            LeadTime = d.LeadTime,
            DcType = d.DcType
        }).ToList()
    };
}
