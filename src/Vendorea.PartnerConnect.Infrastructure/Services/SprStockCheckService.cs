using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <inheritdoc />
public class SprStockCheckService : ISprStockCheckService
{
    private const string SprPartnerCode = "SPR";

    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ISprInteractiveServices _spr;
    private readonly ILogger<SprStockCheckService> _logger;

    public SprStockCheckService(
        ITradingPartnerRepository partnerRepository,
        ITenantPartnerAccountRepository connectionRepository,
        ICredentialProtector credentialProtector,
        ISprInteractiveServices spr,
        ILogger<SprStockCheckService> logger)
    {
        _partnerRepository = partnerRepository;
        _connectionRepository = connectionRepository;
        _credentialProtector = credentialProtector;
        _spr = spr;
        _logger = logger;
    }

    public async Task<StockCheckOutcome> StockCheckAsync(int organizationId, StockCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ItemNumber) || string.IsNullOrWhiteSpace(request.ExternalTenantId))
            return new StockCheckOutcome(StockCheckStatus.InvalidRequest, Error: "ItemNumber and ExternalTenantId are required");

        var partner = await _partnerRepository.GetByCodeAsync(SprPartnerCode, cancellationToken);
        if (partner is null)
            return new StockCheckOutcome(StockCheckStatus.NotConfigured, Error: "SPR trading partner not found");

        var config = SprConfiguration.FromJson(partner.TransportConfigJson);
        var credentials = SprCredentials.FromJson(
            _credentialProtector.Unprotect(partner.TransportCredentialsJson));

        if (string.IsNullOrWhiteSpace(config.WebServicesBaseUrl)
            || string.IsNullOrWhiteSpace(config.WebServicesUserId)
            || string.IsNullOrWhiteSpace(credentials.WebServicesPassword))
        {
            return new StockCheckOutcome(StockCheckStatus.NotConfigured,
                Error: "SPR web services are not configured for this partner");
        }

        // Gate: the dealer must have an effectively-active SPR connection (subscription).
        var connection = await ResolveActiveConnectionAsync(organizationId, partner.Id, request.ExternalTenantId, cancellationToken);
        if (connection is null)
            return new StockCheckOutcome(StockCheckStatus.NoActiveConnection);

        var wsConfig = new SprWebServiceConfig
        {
            BaseUrl = config.WebServicesBaseUrl!,
            GroupCode = config.WebServicesGroupCode ?? string.Empty,
            UserId = config.WebServicesUserId!,
            Password = credentials.WebServicesPassword!,
            CustNumber = connection.AccountNumber,
            TimeoutSeconds = config.WebServicesTimeoutSeconds
        };

        var query = new SprStockCheckQuery
        {
            ItemNumber = request.ItemNumber,
            DcNumbers = request.DcNumbers ?? new List<int>(),
            AvailableOnly = request.AvailableOnly
        };

        // ≤8 specified DCs → Quick Check Plus; otherwise Dealer Stock Check (all DCs + price).
        var result = query.DcNumbers.Count is > 0 and <= 8
            ? await _spr.QuickCheckPlusAsync(wsConfig, query, cancellationToken)
            : await _spr.DealerStockCheckAsync(wsConfig, query, cancellationToken);

        return new StockCheckOutcome(StockCheckStatus.Ok, Map(result));
    }

    private async Task<TenantPartnerAccount?> ResolveActiveConnectionAsync(
        int organizationId, int sprPartnerId, string externalTenantId, CancellationToken cancellationToken)
    {
        var approved = await _connectionRepository.GetConnectionsAsync(
            organizationId, ConnectionApprovalStatus.Approved, cancellationToken);

        return approved.FirstOrDefault(c =>
            c.TradingPartnerId == sprPartnerId
            && string.Equals(c.ExternalTenantId, externalTenantId, StringComparison.OrdinalIgnoreCase)
            && c.Tenant is not null
            && c.Organization is not null
            && EffectiveStatus.IsConnectionEffectivelyActive(c, c.Tenant, c.Organization));
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
