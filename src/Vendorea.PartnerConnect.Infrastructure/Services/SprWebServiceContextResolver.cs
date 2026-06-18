using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

public enum SprContextStatus { Ok, NotConfigured, NoActiveConnection }

public record SprWebServiceContextResult(SprContextStatus Status, SprWebServiceConfig? Config);

/// <summary>
/// Shared resolver for SPR interactive-web-service calls: loads the SPR partner's web-service
/// config + decrypted password and the dealer's effectively-active SPR connection (for the SPR
/// customer number that drives pricing), and enforces that only SPR-subscribed tenants may call.
/// Used by both the stock-check and freight services so the gating logic can't drift.
/// </summary>
public class SprWebServiceContextResolver
{
    private const string SprPartnerCode = "SPR";

    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly ICredentialProtector _credentialProtector;

    public SprWebServiceContextResolver(
        ITradingPartnerRepository partnerRepository,
        ITenantPartnerAccountRepository connectionRepository,
        ICredentialProtector credentialProtector)
    {
        _partnerRepository = partnerRepository;
        _connectionRepository = connectionRepository;
        _credentialProtector = credentialProtector;
    }

    public async Task<SprWebServiceContextResult> ResolveAsync(int organizationId, string externalTenantId, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByCodeAsync(SprPartnerCode, cancellationToken);
        if (partner is null)
            return new SprWebServiceContextResult(SprContextStatus.NotConfigured, null);

        var config = SprConfiguration.FromJson(partner.TransportConfigJson);
        var credentials = SprCredentials.FromJson(_credentialProtector.Unprotect(partner.TransportCredentialsJson));

        if (string.IsNullOrWhiteSpace(config.WebServicesBaseUrl)
            || string.IsNullOrWhiteSpace(config.WebServicesUserId)
            || string.IsNullOrWhiteSpace(credentials.WebServicesPassword))
        {
            return new SprWebServiceContextResult(SprContextStatus.NotConfigured, null);
        }

        var approved = await _connectionRepository.GetConnectionsAsync(
            organizationId, ConnectionApprovalStatus.Approved, cancellationToken);

        var connection = approved.FirstOrDefault(c =>
            c.TradingPartnerId == partner.Id
            && string.Equals(c.ExternalTenantId, externalTenantId, StringComparison.OrdinalIgnoreCase)
            && c.Tenant is not null
            && c.Organization is not null
            && EffectiveStatus.IsConnectionEffectivelyActive(c, c.Tenant, c.Organization));

        if (connection is null)
            return new SprWebServiceContextResult(SprContextStatus.NoActiveConnection, null);

        return new SprWebServiceContextResult(SprContextStatus.Ok, new SprWebServiceConfig
        {
            BaseUrl = config.WebServicesBaseUrl!,
            GroupCode = config.WebServicesGroupCode ?? string.Empty,
            UserId = config.WebServicesUserId!,
            Password = credentials.WebServicesPassword!,
            CustNumber = connection.AccountNumber,
            TimeoutSeconds = config.WebServicesTimeoutSeconds
        });
    }
}
