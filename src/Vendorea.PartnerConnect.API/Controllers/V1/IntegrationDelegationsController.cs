using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Administration of delegated access to a seller account one tenant owns.
/// </summary>
/// <remarks>
/// Admin scope only, and deliberately not reachable with an organization API key. A seller
/// account maps to one Merchant360 tenant; sharing one is an exception that a named person
/// decides, with a recorded reason, and an integration credential must not be able to make that
/// decision on anybody's behalf.
/// </remarks>
[ApiController]
[Route("api/v1/admin/integration-delegations")]
[Produces("application/json")]
[Authorize]
public class IntegrationDelegationsController : ControllerBase
{
    private readonly IIntegrationAccountDelegationService _delegations;
    private readonly ILogger<IntegrationDelegationsController> _logger;

    public IntegrationDelegationsController(
        IIntegrationAccountDelegationService delegations,
        ILogger<IntegrationDelegationsController> logger)
    {
        _delegations = delegations;
        _logger = logger;
    }

    /// <summary>Lists every grant for an account, withdrawn ones included.</summary>
    [HttpGet]
    [RequireScope(ApiScopes.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationDelegationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string providerCode, [FromQuery] string externalAccountId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerCode) || string.IsNullOrWhiteSpace(externalAccountId))
        {
            return UnprocessableEntity(new
            {
                code = "VALIDATION_FAILED",
                message = "providerCode and externalAccountId are required."
            });
        }

        var delegations = await _delegations.ListForAccountAsync(
            providerCode, externalAccountId, cancellationToken);

        return Ok(delegations.Select(Map).ToList());
    }

    /// <summary>Grants a second tenant the right to connect an account another tenant owns.</summary>
    [HttpPost]
    [RequireScope(ApiScopes.Admin)]
    [ProducesResponseType(typeof(IntegrationDelegationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Grant(
        [FromBody] GrantIntegrationDelegationRequest request, CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.ProviderCode)
            || string.IsNullOrWhiteSpace(request.ExternalAccountId)
            || request.DelegateTenantId <= 0
            || string.IsNullOrWhiteSpace(request.GrantedBy))
        {
            return UnprocessableEntity(new
            {
                code = "VALIDATION_FAILED",
                message = "providerCode, externalAccountId, delegateTenantId and grantedBy are required."
            });
        }

        var result = await _delegations.GrantAsync(
            request.ProviderCode, request.ExternalAccountId, request.DelegateTenantId,
            request.GrantedBy, request.Reason, cancellationToken);

        if (!result.Success)
        {
            return UnprocessableEntity(new { code = "DELEGATION_REFUSED", message = result.Error });
        }

        _logger.LogWarning(
            "Delegated access granted for {ProviderCode} account {Account} to tenant {TenantId} by {GrantedBy}.",
            request.ProviderCode, request.ExternalAccountId, request.DelegateTenantId, request.GrantedBy);

        return CreatedAtAction(nameof(List),
            new { providerCode = request.ProviderCode, externalAccountId = request.ExternalAccountId },
            Map(result.Delegation!));
    }

    /// <summary>Withdraws a grant and disconnects the delegate.</summary>
    [HttpDelete("{delegationId:guid}")]
    [RequireScope(ApiScopes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid delegationId, [FromQuery] string? revokedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(revokedBy))
        {
            return UnprocessableEntity(new
            {
                code = "VALIDATION_FAILED",
                message = "revokedBy is required."
            });
        }

        var result = await _delegations.RevokeAsync(delegationId, revokedBy, cancellationToken);

        return result.Success
            ? NoContent()
            : NotFound(new { code = "NOT_FOUND", message = result.Error });
    }

    private static IntegrationDelegationDto Map(
        Domain.Entities.Integration.IntegrationAccountDelegation d) => new()
    {
        Id = d.Id,
        ExternalAccountId = d.ExternalAccountId,
        OwnerTenantId = d.OwnerTenantId,
        DelegateTenantId = d.DelegateTenantId,
        GrantedBy = d.GrantedBy,
        Reason = d.Reason,
        GrantedAt = d.GrantedAt,
        RevokedAt = d.RevokedAt,
        RevokedBy = d.RevokedBy,
        IsActive = d.IsActive
    };
}
