using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for processing canonical supplier order submissions from external platforms.
/// Handles validation, idempotency, and partner resolution.
/// </summary>
public interface ISupplierOrderIntakeService
{
    /// <summary>
    /// Submits a canonical supplier order for processing.
    /// Validates routing context, business data, and partner resolution before acceptance.
    /// </summary>
    Task<SubmitSupplierOrderResponse> SubmitOrderAsync(
        SubmitSupplierOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order by external order ID and source platform.
    /// </summary>
    Task<Order?> GetOrderByExternalIdAsync(
        string sourcePlatform,
        string externalOrderId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for resolving partner-specific requirements from canonical order data.
/// </summary>
public interface IPartnerOrderResolutionService
{
    /// <summary>
    /// Validates that the partner connection can accept orders and has all required configuration.
    /// </summary>
    Task<PartnerResolutionResult> ValidatePartnerConnectionAsync(
        int partnerConnectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves partner-specific order requirements from canonical data.
    /// Returns the resolved context needed for downstream document generation.
    /// </summary>
    Task<PartnerResolutionResult> ResolvePartnerRequirementsAsync(
        SubmitSupplierOrderRequest request,
        TenantPartnerAccount account,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of partner resolution validation.
/// </summary>
public record PartnerResolutionResult
{
    /// <summary>
    /// Whether resolution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The resolved partner account.
    /// </summary>
    public TenantPartnerAccount? Account { get; init; }

    /// <summary>
    /// The trading partner.
    /// </summary>
    public TradingPartner? Partner { get; init; }

    /// <summary>
    /// Partner-specific configuration resolved from account.
    /// </summary>
    public PartnerOrderConfiguration? Configuration { get; init; }

    /// <summary>
    /// Validation errors preventing acceptance.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Warnings that don't prevent acceptance.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PartnerResolutionResult Succeeded(
        TenantPartnerAccount account,
        TradingPartner partner,
        PartnerOrderConfiguration configuration,
        IReadOnlyList<string>? warnings = null)
        => new()
        {
            Success = true,
            Account = account,
            Partner = partner,
            Configuration = configuration,
            Warnings = warnings ?? []
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PartnerResolutionResult Failed(IReadOnlyList<ValidationError> errors)
        => new()
        {
            Success = false,
            Errors = errors
        };

    /// <summary>
    /// Creates a failed result with a single error.
    /// </summary>
    public static PartnerResolutionResult Failed(string code, string field, string message)
        => new()
        {
            Success = false,
            Errors = [new ValidationError(code, field, message)]
        };
}

/// <summary>
/// Partner-specific order configuration resolved from partner account settings.
/// These values are NOT exposed to Merchant360 - they are resolved internally.
/// </summary>
public record PartnerOrderConfiguration
{
    /// <summary>
    /// Partner code (e.g., "SPR").
    /// </summary>
    public string PartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Customer account number at the partner.
    /// </summary>
    public string AccountNumber { get; init; } = string.Empty;

    /// <summary>
    /// Enterprise code for SPR XML (resolved from partner config).
    /// </summary>
    public string? EnterpriseCode { get; init; }

    /// <summary>
    /// Buyer organization code for SPR XML.
    /// </summary>
    public string? BuyerOrgCode { get; init; }

    /// <summary>
    /// Seller organization code for SPR XML.
    /// </summary>
    public string? SellerOrgCode { get; init; }

    /// <summary>
    /// Ship node code (SPR-specific).
    /// </summary>
    public string? ShipNode { get; init; }

    /// <summary>
    /// Whether to require complete shipment (SPR IsShipComplete flag).
    /// Derived from AllowPartialShipment business option.
    /// </summary>
    public bool RequireCompleteShipment { get; init; }

    /// <summary>
    /// Transport type for sending the order (SFTP, AS2, HTTP).
    /// </summary>
    public string TransportType { get; init; } = "SFTP";

    /// <summary>
    /// Outbound document path for the order.
    /// </summary>
    public string? OutboundPath { get; init; }

    /// <summary>
    /// Whether to auto-generate 997 acknowledgment.
    /// </summary>
    public bool AutoSend997 { get; init; } = true;
}
