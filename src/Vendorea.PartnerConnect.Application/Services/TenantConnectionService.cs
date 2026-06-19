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

    public async Task<ConnectionChangeResult> CancelConnectionAsync(
        int organizationId, string externalTenantId, int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        var matches = await FindConnectionsAsync(organizationId, externalTenantId, tradingPartnerId, cancellationToken);
        if (matches.Count == 0)
            return ConnectionChangeResult.NotFound("No connection found for this tenant and partner");

        var pending = matches.FirstOrDefault(c => c.ApprovalStatus == ConnectionApprovalStatus.Pending);
        if (pending != null)
        {
            pending.ApprovalStatus = ConnectionApprovalStatus.Cancelled;
            pending.IsActive = false;
            pending.DecidedAt = DateTime.UtcNow;
            pending.UpdatedAt = DateTime.UtcNow;
            await _connectionRepository.UpdateAsync(pending, cancellationToken);
            _logger.LogInformation(
                "Connection {ConnectionId} cancelled by merchant: org {OrgId}, partner {PartnerId}, tenant org-id {ExternalTenantId}",
                pending.Id, organizationId, tradingPartnerId, externalTenantId);
            return ConnectionChangeResult.Ok(pending);
        }

        // Idempotent: a request that was already cancelled is treated as success.
        var alreadyCancelled = matches.FirstOrDefault(c => c.ApprovalStatus == ConnectionApprovalStatus.Cancelled);
        if (alreadyCancelled != null)
            return ConnectionChangeResult.Ok(alreadyCancelled);

        return ConnectionChangeResult.Invalid(
            "Connection is not pending; only a pending request can be cancelled (use unsubscribe for an approved connection)");
    }

    public async Task<ConnectionChangeResult> UnsubscribeConnectionAsync(
        int organizationId, string externalTenantId, int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        var matches = await FindConnectionsAsync(organizationId, externalTenantId, tradingPartnerId, cancellationToken);
        if (matches.Count == 0)
            return ConnectionChangeResult.NotFound("No connection found for this tenant and partner");

        var approved = matches.FirstOrDefault(c => c.ApprovalStatus == ConnectionApprovalStatus.Approved);
        if (approved != null)
        {
            approved.ApprovalStatus = ConnectionApprovalStatus.Unsubscribed;
            approved.IsActive = false;
            approved.DecidedAt = DateTime.UtcNow;
            approved.UpdatedAt = DateTime.UtcNow;
            await _connectionRepository.UpdateAsync(approved, cancellationToken);
            _logger.LogInformation(
                "Connection {ConnectionId} unsubscribed by merchant: org {OrgId}, partner {PartnerId}, tenant org-id {ExternalTenantId}",
                approved.Id, organizationId, tradingPartnerId, externalTenantId);
            return ConnectionChangeResult.Ok(approved);
        }

        // Idempotent: an already-unsubscribed connection is treated as success.
        var alreadyUnsubscribed = matches.FirstOrDefault(c => c.ApprovalStatus == ConnectionApprovalStatus.Unsubscribed);
        if (alreadyUnsubscribed != null)
            return ConnectionChangeResult.Ok(alreadyUnsubscribed);

        if (matches.Any(c => c.ApprovalStatus == ConnectionApprovalStatus.Pending))
            return ConnectionChangeResult.Invalid("Connection is still pending; cancel the request instead of unsubscribing");

        return ConnectionChangeResult.Invalid("No active subscription to unsubscribe");
    }

    /// <summary>
    /// Finds the connection rows for an (organization, merchant tenant, PartnerConnect partner) tuple.
    /// ExternalTenantId is M360's tenant id; TradingPartnerId is PartnerConnect's own id (the value
    /// returned by GET /api/v1/org/partners), NOT the caller's local partner id.
    /// </summary>
    private async Task<List<TenantPartnerAccount>> FindConnectionsAsync(
        int organizationId, string externalTenantId, int tradingPartnerId, CancellationToken cancellationToken)
    {
        var all = await _connectionRepository.GetConnectionsAsync(organizationId, null, cancellationToken);
        return all
            .Where(c => c.TradingPartnerId == tradingPartnerId
                && string.Equals(c.ExternalTenantId, externalTenantId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
