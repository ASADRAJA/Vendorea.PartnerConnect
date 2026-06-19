namespace Vendorea.PartnerConnect.Contracts.Integration;

/// <summary>
/// Canonical request for submitting a supplier order through PartnerConnect.
/// This contract is partner-agnostic; M360 submits business intent, not EDI/XML details.
/// </summary>
public record SubmitSupplierOrderRequest
{
    // ===== ROUTING CONTEXT (Required) =====

    /// <summary>
    /// Source platform identifier (e.g., "Merchant360", "DirectAPI").
    /// Required - identifies which system is submitting this order.
    /// </summary>
    public string SourcePlatform { get; init; } = string.Empty;

    /// <summary>
    /// Organization ID in PartnerConnect.
    /// Required - the billable account holder. May be omitted if <see cref="OrganizationCode"/> is supplied.
    /// </summary>
    public int OrganizationId { get; init; }

    /// <summary>
    /// The organization's PartnerConnect code (e.g. "ORG-00001"). When supplied, the org is
    /// resolved by code instead of <see cref="OrganizationId"/> — the preferred external identifier.
    /// </summary>
    public string? OrganizationCode { get; init; }

    /// <summary>
    /// The source platform's own merchant id (e.g. M360's merchant id). PartnerConnect resolves
    /// the tenant by matching this against Tenant.ExternalId — it is NOT PC's internal Tenant.Id.
    /// Required - identifies the merchant placing the order.
    /// </summary>
    public int MerchantId { get; init; }

    /// <summary>
    /// Partner connection ID in PartnerConnect.
    /// Required - identifies which trading partner and account to use.
    /// </summary>
    public int PartnerConnectionId { get; init; }

    /// <summary>
    /// External order ID from the source platform (e.g., M360 order ID).
    /// Required - enables correlation between platforms.
    /// </summary>
    public string ExternalOrderId { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// Required - tracks the request across system boundaries.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Idempotency key for duplicate submission detection.
    /// Required - prevents duplicate order creation on retry.
    /// </summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// Optional metadata about who submitted this order (username, service account, etc.).
    /// </summary>
    public string? SubmittedBy { get; init; }

    // ===== ORDER HEADER (Business Data) =====

    /// <summary>
    /// Customer/dealer purchase order number.
    /// Required - the merchant's reference number for this order.
    /// </summary>
    public string PoNumber { get; init; } = string.Empty;

    /// <summary>
    /// Order date (when the order was placed by the merchant).
    /// Optional - defaults to current UTC time if not provided.
    /// </summary>
    public DateTime? OrderDate { get; init; }

    /// <summary>
    /// Optional notes or special instructions.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Optional external references (JSON object for additional identifiers).
    /// </summary>
    public string? ExternalReferences { get; init; }

    // ===== SHIP-TO ADDRESS =====

    /// <summary>
    /// Ship-to address details.
    /// Required - where the order should be shipped.
    /// </summary>
    public CanonicalAddressInfo? ShipTo { get; init; }

    /// <summary>
    /// Bill-to address details (optional, may default to ship-to).
    /// </summary>
    public CanonicalAddressInfo? BillTo { get; init; }

    /// <summary>
    /// Ship-from business details (the merchant/dealer business shown as the label's ship-from).
    /// Optional - emitted as PersonInfoContact (business name + address). The dealer's logo, phone,
    /// and website are NOT sent here; SPR pulls those from the dealer's stored label profile.
    /// </summary>
    public CanonicalAddressInfo? ShipFrom { get; init; }

    // ===== ORDER LINES =====

    /// <summary>
    /// Order line items.
    /// Required - at least one line is required.
    /// </summary>
    public IReadOnlyList<CanonicalOrderLineRequest> Lines { get; init; } = [];

    // ===== BUSINESS OPTIONS =====

    /// <summary>
    /// Order type indicating fulfillment model.
    /// Values: "WrapAndLabel" (default), "StockOrder", "DropShip".
    /// - StockOrder: Ship to dealer's location (standard replenishment) → SPR order type 01
    /// - WrapAndLabel: Ship with dealer branding/packaging and a customer-facing label → SPR order type 03
    /// - DropShip: Ship directly to the end customer → SPR order type 04
    /// When omitted, defaults to WrapAndLabel (SPR order type 03).
    /// </summary>
    public string OrderType { get; init; } = "WrapAndLabel";

    /// <summary>
    /// Ship-from distribution center code (the SPR DC the order ships from, e.g. "8").
    /// Optional - emitted as Order/@ShipNode. When omitted, SPR selects the DC.
    /// </summary>
    public string? DistributionCenterCode { get; init; }

    /// <summary>
    /// Attention line for the shipping label (e.g. a contact or department name).
    /// Optional - emitted as the SPR DealerAttn label field.
    /// </summary>
    public string? Attn { get; init; }

    /// <summary>
    /// Dealer-entered comment lines printed on the shipping label (up to 3 lines).
    /// Optional - emitted as the SPR LabelCmmnts1..3 label fields (each truncated to 25 chars).
    /// </summary>
    public IReadOnlyList<string>? LabelComments { get; init; }

    /// <summary>
    /// Allow partial shipment of order (default: true).
    /// If false, order must be shipped complete.
    /// </summary>
    public bool AllowPartialShipment { get; init; } = true;

    /// <summary>
    /// Allow backordering of out-of-stock items (default: true).
    /// If false, unavailable items will be rejected/cancelled.
    /// </summary>
    public bool AllowBackorder { get; init; } = true;

    /// <summary>
    /// Allow product substitutions (default: false).
    /// If true, partner may substitute equivalent products.
    /// </summary>
    public bool AllowSubstitutions { get; init; } = false;

    /// <summary>
    /// Shipping priority preference.
    /// Values: "Standard" (default), "Expedited", "NextDay", "Freight".
    /// </summary>
    public string? ShippingPriority { get; init; }

    /// <summary>
    /// Requested ship date.
    /// </summary>
    public DateTime? RequestedShipDate { get; init; }

    /// <summary>
    /// Requested delivery date.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; init; }

    /// <summary>
    /// Shipping method preference (canonical code like "Ground", "Express", "Freight").
    /// </summary>
    public string? ShippingMethod { get; init; }
}

/// <summary>
/// Canonical address information for supplier orders.
/// </summary>
public record CanonicalAddressInfo
{
    /// <summary>
    /// Contact name or attention line.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Company or business name.
    /// </summary>
    public string? Company { get; init; }

    /// <summary>
    /// Primary address line.
    /// Required for ship-to addresses.
    /// </summary>
    public string? Address1 { get; init; }

    /// <summary>
    /// Secondary address line (suite, unit, etc.).
    /// </summary>
    public string? Address2 { get; init; }

    /// <summary>
    /// Third address line (rarely used; maps to SPR AddressLine3).
    /// </summary>
    public string? Address3 { get; init; }

    /// <summary>
    /// City name.
    /// Required for ship-to addresses.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// State/province code (2-letter for US/CA).
    /// Required for ship-to addresses.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Postal/ZIP code.
    /// Required for ship-to addresses.
    /// </summary>
    public string? PostalCode { get; init; }

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2, defaults to "US").
    /// </summary>
    public string Country { get; init; } = "US";

    /// <summary>
    /// Phone number for delivery contact.
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// Email address for delivery notifications.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether this is a residential address (affects shipping options).
    /// </summary>
    public bool? IsResidential { get; init; }
}

/// <summary>
/// Canonical order line item for supplier orders.
/// </summary>
public record CanonicalOrderLineRequest
{
    /// <summary>
    /// Line number (1-based, unique within order).
    /// If not provided, will be auto-assigned.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Partner/vendor SKU (the supplier's item identifier).
    /// Required - must be resolvable by the trading partner.
    /// </summary>
    public string VendorSku { get; init; } = string.Empty;

    /// <summary>
    /// Buyer's internal SKU (merchant's item identifier).
    /// Optional - for reference/tracking purposes.
    /// </summary>
    public string? BuyerSku { get; init; }

    /// <summary>
    /// UPC/EAN barcode if known.
    /// Optional - may help with item identification.
    /// </summary>
    public string? Upc { get; init; }

    /// <summary>
    /// Quantity ordered.
    /// Required - must be greater than 0.
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Unit of measure (canonical codes: "EA", "CS", "PK", "PL", "LB", "KG").
    /// Optional - defaults to "EA" (each).
    /// </summary>
    public string UnitOfMeasure { get; init; } = "EA";

    /// <summary>
    /// Unit price for this line.
    /// Optional - if not provided, partner's current price may be used.
    /// </summary>
    public decimal? UnitPrice { get; init; }

    /// <summary>
    /// Item description (for reference, not sent to partner).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Line-level requested delivery date (overrides order-level).
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; init; }

    /// <summary>
    /// Line-level notes/special instructions.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// External line reference from source platform.
    /// </summary>
    public string? ExternalLineReference { get; init; }
}

/// <summary>
/// Response from submitting a supplier order.
/// </summary>
public record SubmitSupplierOrderResponse
{
    /// <summary>
    /// Whether the submission was accepted.
    /// </summary>
    public bool Accepted { get; init; }

    /// <summary>
    /// PartnerConnect integration/order ID for tracking.
    /// </summary>
    public int? PartnerConnectOrderId { get; init; }

    /// <summary>
    /// Partner document ID if document was created immediately.
    /// </summary>
    public int? PartnerDocumentId { get; init; }

    /// <summary>
    /// Current status of the order.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the order was accepted.
    /// </summary>
    public DateTime? AcceptedAt { get; init; }

    /// <summary>
    /// Validation warnings (order accepted but with notes).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Validation errors (order not accepted).
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Whether this was a duplicate submission (idempotent).
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static SubmitSupplierOrderResponse Success(
        int orderId,
        string correlationId,
        int? documentId = null,
        IReadOnlyList<string>? warnings = null,
        bool isDuplicate = false)
        => new()
        {
            Accepted = true,
            PartnerConnectOrderId = orderId,
            PartnerDocumentId = documentId,
            Status = isDuplicate ? "Duplicate" : "Accepted",
            CorrelationId = correlationId,
            AcceptedAt = DateTime.UtcNow,
            Warnings = warnings ?? [],
            IsDuplicate = isDuplicate
        };

    /// <summary>
    /// Creates a validation failure response.
    /// </summary>
    public static SubmitSupplierOrderResponse ValidationFailed(
        string correlationId,
        IReadOnlyList<ValidationError> errors)
        => new()
        {
            Accepted = false,
            Status = "ValidationFailed",
            CorrelationId = correlationId,
            Errors = errors
        };

    /// <summary>
    /// Creates a conflict response for mismatched duplicate.
    /// </summary>
    public static SubmitSupplierOrderResponse Conflict(
        string correlationId,
        int existingOrderId)
        => new()
        {
            Accepted = false,
            PartnerConnectOrderId = existingOrderId,
            Status = "Conflict",
            CorrelationId = correlationId,
            Errors = [new ValidationError("IDEMPOTENCY_CONFLICT", "IdempotencyKey", "Order already exists with different content")]
        };
}

/// <summary>
/// Validation error details.
/// </summary>
public record ValidationError(string Code, string Field, string Message);
