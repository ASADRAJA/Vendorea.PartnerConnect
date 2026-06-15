using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class TenantConnectionService : ITenantConnectionService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantPartnerAccountRepository _connectionRepository;
    private readonly ILogger<TenantConnectionService> _logger;

    public TenantConnectionService(
        IOrganizationRepository organizationRepository,
        ITenantPartnerAccountRepository connectionRepository,
        ILogger<TenantConnectionService> logger)
    {
        _organizationRepository = organizationRepository;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    public async Task<RequestConnectionResult> RequestConnectionAsync(
        int organizationId, RequestConnectionInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.ExternalTenantId))
            return RequestConnectionResult.Failed("Tenant's org id is required");
        if (string.IsNullOrWhiteSpace(input.AccountNumber))
            return RequestConnectionResult.Failed("Partner account number is required");

        var org = await _organizationRepository.GetByIdWithPartnersAsync(organizationId, cancellationToken);
        if (org is null)
            return RequestConnectionResult.Failed("Organization not found");
        if (!EffectiveStatus.IsOrganizationActive(org))
            return RequestConnectionResult.Failed("Organization is not active");
        if (org.Partners.All(p => p.TradingPartnerId != input.TradingPartnerId))
            return RequestConnectionResult.Failed("Partner is not enabled for this organization");

        if (await _connectionRepository.ConnectionExistsAsync(
                organizationId, input.ExternalTenantId, input.TradingPartnerId, cancellationToken))
        {
            return RequestConnectionResult.Failed("A connection already exists for this tenant and partner");
        }

        var connection = new TenantPartnerAccount
        {
            OrganizationId = organizationId,
            ExternalTenantId = input.ExternalTenantId,
            TradingPartnerId = input.TradingPartnerId,
            AccountNumber = input.AccountNumber,
            ContactFirstName = input.ContactFirstName,
            ContactLastName = input.ContactLastName,
            SpecialIdentifyingCode = input.SpecialIdentifyingCode,
            Notes = input.Notes,
            ConfirmationFieldsJson = input.ConfirmationFields is { Count: > 0 }
                ? JsonSerializer.Serialize(input.ConfirmationFields)
                : null,
            ApprovalStatus = ConnectionApprovalStatus.Pending,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _connectionRepository.AddAsync(connection, cancellationToken);
        _logger.LogInformation(
            "Connection {ConnectionId} requested: org {OrgId}, partner {PartnerId}, tenant org-id {ExternalTenantId}",
            created.Id, organizationId, input.TradingPartnerId, input.ExternalTenantId);

        return RequestConnectionResult.Succeeded(created);
    }
}
