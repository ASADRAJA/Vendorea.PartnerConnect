using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Security;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class OrgApiKeyAuthenticator : IOrgApiKeyAuthenticator
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrgApiKeyAuthenticator(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<Organization?> ResolveActiveOrganizationAsync(string? apiKey, CancellationToken cancellationToken = default)
    {
        var hash = ApiKeyHasher.Hash(apiKey);
        if (hash is null)
            return null;

        var org = await _organizationRepository.GetByPortalApiKeyHashAsync(hash, cancellationToken);
        if (org is null || !EffectiveStatus.IsOrganizationActive(org))
            return null;

        return org;
    }
}
