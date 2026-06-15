using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Authenticates an organization's inbound call by its API key (the same key PartnerConnect uses
/// for outbound portal callbacks). Returns the org only when the key matches an Active organization.
/// </summary>
public interface IOrgApiKeyAuthenticator
{
    Task<Organization?> ResolveActiveOrganizationAsync(string? apiKey, CancellationToken cancellationToken = default);
}
