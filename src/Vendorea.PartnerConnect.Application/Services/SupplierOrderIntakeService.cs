using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for processing canonical supplier order submissions from external platforms.
/// Implements validation, idempotency, partner resolution, and order creation.
/// </summary>
public class SupplierOrderIntakeService : ISupplierOrderIntakeService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantPartnerAccountRepository _accountRepository;
    private readonly IPartnerOrderResolutionService _resolutionService;
    private readonly ILogger<SupplierOrderIntakeService> _logger;

    public SupplierOrderIntakeService(
        IOrderRepository orderRepository,
        IOrganizationRepository organizationRepository,
        ITenantRepository tenantRepository,
        ITenantPartnerAccountRepository accountRepository,
        IPartnerOrderResolutionService resolutionService,
        ILogger<SupplierOrderIntakeService> logger)
    {
        _orderRepository = orderRepository;
        _organizationRepository = organizationRepository;
        _tenantRepository = tenantRepository;
        _accountRepository = accountRepository;
        _resolutionService = resolutionService;
        _logger = logger;
    }

    public async Task<SubmitSupplierOrderResponse> SubmitOrderAsync(
        SubmitSupplierOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = !string.IsNullOrWhiteSpace(request.CorrelationId)
            ? request.CorrelationId
            : Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Processing supplier order submission from {SourcePlatform}, ExternalOrderId={ExternalOrderId}, CorrelationId={CorrelationId}",
            request.SourcePlatform, request.ExternalOrderId, correlationId);

        // Step 1: Validate routing context completeness
        var routingErrors = ValidateRoutingContext(request);
        if (routingErrors.Count > 0)
        {
            _logger.LogWarning(
                "Routing context validation failed for CorrelationId={CorrelationId}: {Errors}",
                correlationId, string.Join(", ", routingErrors.Select(e => e.Code)));
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, routingErrors);
        }

        // Step 2: Validate business fields completeness
        var businessErrors = ValidateBusinessFields(request);
        if (businessErrors.Count > 0)
        {
            _logger.LogWarning(
                "Business field validation failed for CorrelationId={CorrelationId}: {Errors}",
                correlationId, string.Join(", ", businessErrors.Select(e => e.Code)));
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, businessErrors);
        }

        // Step 3: Check idempotency
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingOrder = await FindByIdempotencyKeyAsync(
                request.OrganizationId, request.IdempotencyKey, cancellationToken);

            if (existingOrder != null)
            {
                // Check if it's truly a duplicate (same content) or a conflict
                if (IsSameOrderContent(existingOrder, request))
                {
                    _logger.LogInformation(
                        "Idempotent duplicate detected for IdempotencyKey={IdempotencyKey}, returning existing OrderId={OrderId}",
                        request.IdempotencyKey, existingOrder.Id);

                    return SubmitSupplierOrderResponse.Success(
                        existingOrder.Id,
                        existingOrder.CorrelationId.ToString(),
                        existingOrder.EdiDocumentId,
                        isDuplicate: true);
                }
                else
                {
                    _logger.LogWarning(
                        "Idempotency conflict for IdempotencyKey={IdempotencyKey}, existing OrderId={OrderId} has different content",
                        request.IdempotencyKey, existingOrder.Id);

                    return SubmitSupplierOrderResponse.Conflict(correlationId, existingOrder.Id);
                }
            }
        }

        // Step 4: Validate organization exists and is active. Prefer the org Code (the external
        // identifier) when supplied; otherwise fall back to the internal OrganizationId.
        var org = !string.IsNullOrWhiteSpace(request.OrganizationCode)
            ? await _organizationRepository.GetByCodeAsync(request.OrganizationCode, cancellationToken)
            : await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (org == null)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, [
                new ValidationError("ORGANIZATION_NOT_FOUND", "OrganizationId", "Organization not found")
            ]);
        }
        if (org.Status != OrganizationStatus.Active)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, [
                new ValidationError("ORGANIZATION_NOT_ACTIVE", "OrganizationId", "Organization is not active")
            ]);
        }

        // Step 5: Resolve the tenant scoped to the org. MerchantId is the org's own id for the
        // tenant, stored as Tenant.ExternalId — and it's only unique WITHIN an org, so we must
        // scope the lookup by org to avoid cross-org collisions.
        var tenant = await _tenantRepository.GetByOrgAndExternalIdAsync(
            org.Id, request.MerchantId.ToString(), cancellationToken);
        if (tenant == null)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, [
                new ValidationError("MERCHANT_NOT_FOUND", "MerchantId", "Merchant/tenant not found for the supplied merchant id")
            ]);
        }
        if (tenant.Status != TenantStatus.Active)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, [
                new ValidationError("MERCHANT_NOT_ACTIVE", "MerchantId", "Merchant is not active")
            ]);
        }

        // Step 6: Validate partner connection and resolve partner requirements
        var resolutionResult = await _resolutionService.ValidatePartnerConnectionAsync(
            request.PartnerConnectionId, cancellationToken);

        if (!resolutionResult.Success)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, resolutionResult.Errors);
        }

        // Enforce the effective-status chain: the connection must belong to the resolved (active)
        // tenant. Org-active (step 4) and tenant-active (step 5) are already checked; this closes
        // the cross-tenant gap so a connection from another tenant can't be used to place orders.
        if (resolutionResult.Account!.TenantId != tenant.Id)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, [
                new ValidationError("PARTNER_CONNECTION_TENANT_MISMATCH", "PartnerConnectionId",
                    "Partner connection does not belong to the specified merchant/tenant")
            ]);
        }

        // Step 7: Validate partner-specific requirements
        var requirementsResult = await _resolutionService.ResolvePartnerRequirementsAsync(
            request, resolutionResult.Account!, cancellationToken);

        if (!requirementsResult.Success)
        {
            return SubmitSupplierOrderResponse.ValidationFailed(correlationId, requirementsResult.Errors);
        }

        // Step 8: Create the order (using the resolved internal tenant id, not the M360 merchant id)
        var order = CreateOrderFromRequest(
            request,
            tenant.Id,
            Guid.Parse(correlationId.Length == 36 ? correlationId : Guid.NewGuid().ToString()),
            resolutionResult.Account!,
            requirementsResult.Configuration!);

        await _orderRepository.AddAsync(order, cancellationToken);

        // Step 9: Record initial status history
        await _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = null,
            ToStatus = OrderStatus.Submitted,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = request.SubmittedBy ?? request.SourcePlatform,
            Source = request.SourcePlatform,
            Reason = "Order submitted via integration API"
        }, cancellationToken);

        _logger.LogInformation(
            "Created order OrderId={OrderId} for CorrelationId={CorrelationId}, Partner={PartnerCode}",
            order.Id, correlationId, requirementsResult.Configuration!.PartnerCode);

        // Collect all warnings
        var allWarnings = new List<string>();
        allWarnings.AddRange(resolutionResult.Warnings);
        allWarnings.AddRange(requirementsResult.Warnings);

        return SubmitSupplierOrderResponse.Success(
            order.Id,
            order.CorrelationId.ToString(),
            warnings: allWarnings);
    }

    public async Task<Order?> GetOrderByExternalIdAsync(
        string sourcePlatform,
        string externalOrderId,
        CancellationToken cancellationToken = default)
    {
        // This requires a new repository method
        var orders = await _orderRepository.GetAllAsync(
            filter: o => o.SourcePlatform == sourcePlatform && o.ExternalOrderId == externalOrderId,
            limit: 1,
            cancellationToken: cancellationToken);

        return orders.FirstOrDefault();
    }

    private async Task<Order?> FindByIdempotencyKeyAsync(
        int organizationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _orderRepository.GetByIdempotencyKeyAsync(
            organizationId, idempotencyKey, cancellationToken);
    }

    private static List<ValidationError> ValidateRoutingContext(SubmitSupplierOrderRequest request)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.SourcePlatform))
            errors.Add(new ValidationError("REQUIRED_FIELD", "SourcePlatform", "Source platform is required"));

        if (request.OrganizationId <= 0)
            errors.Add(new ValidationError("REQUIRED_FIELD", "OrganizationId", "Organization ID is required"));

        if (request.MerchantId <= 0)
            errors.Add(new ValidationError("REQUIRED_FIELD", "MerchantId", "Merchant ID is required"));

        if (request.PartnerConnectionId <= 0)
            errors.Add(new ValidationError("REQUIRED_FIELD", "PartnerConnectionId", "Partner connection ID is required"));

        if (string.IsNullOrWhiteSpace(request.ExternalOrderId))
            errors.Add(new ValidationError("REQUIRED_FIELD", "ExternalOrderId", "External order ID is required"));

        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            errors.Add(new ValidationError("REQUIRED_FIELD", "CorrelationId", "Correlation ID is required"));

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            errors.Add(new ValidationError("REQUIRED_FIELD", "IdempotencyKey", "Idempotency key is required"));

        return errors;
    }

    private static List<ValidationError> ValidateBusinessFields(SubmitSupplierOrderRequest request)
    {
        var errors = new List<ValidationError>();

        // PO Number is required
        if (string.IsNullOrWhiteSpace(request.PoNumber))
            errors.Add(new ValidationError("REQUIRED_FIELD", "PoNumber", "Purchase order number is required"));

        // At least one line is required
        if (request.Lines.Count == 0)
            errors.Add(new ValidationError("REQUIRED_FIELD", "Lines", "At least one order line is required"));

        // Validate each line
        for (int i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];

            if (string.IsNullOrWhiteSpace(line.VendorSku))
                errors.Add(new ValidationError("REQUIRED_FIELD", $"Lines[{i}].VendorSku", $"Vendor SKU is required on line {i + 1}"));

            if (line.Quantity <= 0)
                errors.Add(new ValidationError("INVALID_VALUE", $"Lines[{i}].Quantity", $"Quantity must be greater than 0 on line {i + 1}"));
        }

        // Ship-to is generally required
        if (request.ShipTo == null)
            errors.Add(new ValidationError("REQUIRED_FIELD", "ShipTo", "Ship-to address is required"));

        return errors;
    }

    private static bool IsSameOrderContent(Order existing, SubmitSupplierOrderRequest request)
    {
        // Compare key fields to determine if this is truly a duplicate
        // or a conflicting submission with the same idempotency key
        return existing.PoNumber == request.PoNumber &&
               existing.TradingPartnerId == existing.TradingPartnerId && // from account
               existing.Lines.Count == request.Lines.Count;
        // A more thorough comparison could compare line items
    }

    private static Order CreateOrderFromRequest(
        SubmitSupplierOrderRequest request,
        int tenantId,
        Guid correlationId,
        TenantPartnerAccount account,
        PartnerOrderConfiguration config)
    {
        var order = new Order
        {
            OrganizationId = request.OrganizationId,
            TenantId = tenantId,
            TradingPartnerId = account.TradingPartnerId,
            TenantPartnerAccountId = account.Id,

            // Integration tracking
            SourcePlatform = request.SourcePlatform,
            ExternalOrderId = request.ExternalOrderId,
            CorrelationId = correlationId,
            IdempotencyKey = request.IdempotencyKey,
            SubmittedBy = request.SubmittedBy,
            ExternalReferencesJson = request.ExternalReferences,

            // Business options
            OrderType = request.OrderType,
            AllowPartialShipment = request.AllowPartialShipment,
            AllowBackorder = request.AllowBackorder,
            AllowSubstitutions = request.AllowSubstitutions,
            FulfillmentPreference = request.ShippingPriority,  // Map ShippingPriority → FulfillmentPreference

            // Order header
            PoNumber = request.PoNumber,
            OrderDate = request.OrderDate ?? DateTime.UtcNow,
            RequestedShipDate = request.RequestedShipDate,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            ShippingMethod = request.ShippingMethod,
            Notes = request.Notes,

            // Addresses as JSON
            ShipToJson = request.ShipTo != null ? JsonSerializer.Serialize(request.ShipTo) : null,
            BillToJson = request.BillTo != null ? JsonSerializer.Serialize(request.BillTo) : null,

            // Status
            Status = OrderStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Create order lines
        int lineNumber = 1;
        foreach (var lineRequest in request.Lines)
        {
            var line = new OrderLine
            {
                LineNumber = lineRequest.LineNumber ?? lineNumber++,
                Sku = lineRequest.BuyerSku ?? lineRequest.VendorSku,
                VendorSku = lineRequest.VendorSku,
                Upc = lineRequest.Upc,
                Description = lineRequest.Description,
                Quantity = lineRequest.Quantity,
                UnitOfMeasure = lineRequest.UnitOfMeasure,
                UnitPrice = lineRequest.UnitPrice ?? 0,
                LineTotal = lineRequest.Quantity * (lineRequest.UnitPrice ?? 0),
                Status = OrderLineStatus.Pending,
                Notes = lineRequest.Notes,
                CreatedAt = DateTime.UtcNow
            };
            order.Lines.Add(line);
        }

        // Calculate totals
        order.SubTotal = order.Lines.Sum(l => l.LineTotal);
        order.TotalAmount = order.SubTotal; // Tax and shipping calculated later

        return order;
    }
}
