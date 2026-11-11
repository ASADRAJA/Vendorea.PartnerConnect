using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Read access to synchronised marketplace sales orders.
/// </summary>
/// <remarks>
/// Read-only, and read-only on purpose. PartnerConnect ingests what the marketplace says and
/// Merchant360 decides what to do about it; an endpoint here that let a caller change an order
/// would put two systems in charge of the same record.
///
/// Every route is tenant-scoped and returns 404 rather than 403 for a tenant outside the
/// caller's organization, so tenant ids cannot be probed.
///
/// No response carries buyer identity or address — see <see cref="SalesOrderDto"/>.
/// </remarks>
[ApiController]
[Route("api/v1/org/tenants/{tenantId:int}/sales-orders")]
[Produces("application/json")]
[Authorize]
public class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderRepository _orders;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IOrgApiKeyAuthenticator _authenticator;

    public SalesOrdersController(
        ISalesOrderRepository orders,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IOrgApiKeyAuthenticator authenticator)
    {
        _orders = orders;
        _organizations = organizations;
        _tenants = tenants;
        _authenticator = authenticator;
    }

    /// <summary>Lists a tenant's sales orders, newest purchase first.</summary>
    [HttpGet]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(SalesOrderPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> List(
        int tenantId,
        [FromQuery] Guid? connectionId,
        [FromQuery] string? status,
        [FromQuery] string? mappingStatus,
        [FromQuery] DateTime? updatedSince,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        // An unrecognised filter value is refused rather than ignored. Silently returning
        // everything because "Shipped" is not a status we know would look like a working filter
        // reporting that nothing matched — or worse, that everything did.
        if (!TryParse<SalesOrderStatus>(status, out var parsedStatus))
        {
            return InvalidFilter(nameof(status), status!, Enum.GetNames<SalesOrderStatus>());
        }

        if (!TryParse<MappingStatus>(mappingStatus, out var parsedMapping))
        {
            return InvalidFilter(nameof(mappingStatus), mappingStatus!, Enum.GetNames<MappingStatus>());
        }

        var page = await _orders.SearchAsync(
            tenantId,
            new SalesOrderQuery
            {
                ConnectionId = connectionId,
                Status = parsedStatus,
                MappingStatus = parsedMapping,
                UpdatedSince = updatedSince,
                Skip = skip,
                Take = take
            },
            cancellationToken);

        return Ok(new SalesOrderPageDto
        {
            Items = page.Items.Select(SalesOrderMapper.Map).ToList(),
            Total = page.Total,
            Skip = page.Skip,
            Take = page.Take
        });
    }

    /// <summary>One order with its lines and sync metadata.</summary>
    [HttpGet("{pcSalesOrderId:guid}")]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(SalesOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int tenantId, Guid pcSalesOrderId, CancellationToken cancellationToken)
    {
        var (org, error) = await ResolveOrgAsync(cancellationToken);
        if (org is null) return error!;

        var tenantError = await EnsureTenantInScopeAsync(org, tenantId, cancellationToken);
        if (tenantError is not null) return tenantError;

        // Scoped by tenant in the query itself, so another tenant's order is not found rather
        // than found and then refused.
        var order = await _orders.GetAsync(tenantId, pcSalesOrderId, cancellationToken);

        return order is null
            ? NotFound(new { code = "NOT_FOUND", message = "Sales order not found" })
            : Ok(SalesOrderMapper.MapDetail(order));
    }

    // ---- helpers ----

    /// <summary>Parses an optional enum filter. Null stays null; nonsense is a failure.</summary>
    private static bool TryParse<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            parsed = result;
            return true;
        }

        return false;
    }

    private IActionResult InvalidFilter(string name, string value, IEnumerable<string> allowed)
        => UnprocessableEntity(new
        {
            code = "VALIDATION_FAILED",
            message = $"'{value}' is not a valid {name}.",
            allowed = allowed.ToArray()
        });

    private async Task<(Organization? Org, IActionResult? Error)> ResolveOrgAsync(
        CancellationToken cancellationToken)
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var org = await _authenticator.ResolveActiveOrganizationAsync(apiKey, cancellationToken);

        return org is null
            ? (null, Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid or missing API key" }))
            : (org, null);
    }

    /// <summary>404, never 403, when the tenant is not the caller's.</summary>
    private async Task<IActionResult?> EnsureTenantInScopeAsync(
        Organization org, int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        return tenant is null || tenant.OrganizationId != org.Id
            ? NotFound(new { code = "NOT_FOUND", message = $"Tenant '{tenantId}' not found" })
            : null;
    }
}
