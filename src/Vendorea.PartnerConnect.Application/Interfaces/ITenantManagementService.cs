using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for tenant management operations.
/// </summary>
public interface ITenantManagementService
{
    /// <summary>
    /// Creates a new organization.
    /// </summary>
    Task<OrganizationResult> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an organization by ID.
    /// </summary>
    Task<Organization?> GetOrganizationAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an organization by code.
    /// </summary>
    Task<Organization?> GetOrganizationByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates organization status.
    /// </summary>
    Task<OrganizationResult> UpdateOrganizationStatusAsync(int id, OrganizationStatus status, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    Task<TenantResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    Task<Tenant?> GetTenantAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tenants for an organization.
    /// </summary>
    Task<IReadOnlyList<Tenant>> GetTenantsAsync(int organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a partner account to a tenant.
    /// </summary>
    Task<TenantPartnerAccountResult> LinkPartnerAccountAsync(LinkPartnerAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets partner accounts for a tenant.
    /// </summary>
    Task<IReadOnlyList<TenantPartnerAccount>> GetPartnerAccountsAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a tenant can place orders (org active, tenant active, account exists).
    /// </summary>
    Task<OrderContextValidationResult> ValidateOrderContextAsync(
        int organizationId,
        int tenantId,
        int tradingPartnerId,
        string accountNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create an organization.
/// </summary>
public record CreateOrganizationRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsMultiTenant { get; init; }
    public string? BillingPlanId { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string Country { get; init; } = "US";
}

/// <summary>
/// Result of an organization operation.
/// </summary>
public record OrganizationResult
{
    public bool Success { get; init; }
    public Organization? Organization { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrganizationResult Succeeded(Organization org) => new() { Success = true, Organization = org };
    public static OrganizationResult Failed(string errorCode, string errorMessage) => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Request to create a tenant.
/// </summary>
public record CreateTenantRequest
{
    public int OrganizationId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? ExternalId { get; init; }
}

/// <summary>
/// Result of a tenant operation.
/// </summary>
public record TenantResult
{
    public bool Success { get; init; }
    public Tenant? Tenant { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static TenantResult Succeeded(Tenant tenant) => new() { Success = true, Tenant = tenant };
    public static TenantResult Failed(string errorCode, string errorMessage) => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Request to link a partner account to a tenant.
/// </summary>
public record LinkPartnerAccountRequest
{
    public int TenantId { get; init; }
    public int TradingPartnerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// Result of a tenant partner account operation.
/// </summary>
public record TenantPartnerAccountResult
{
    public bool Success { get; init; }
    public TenantPartnerAccount? Account { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static TenantPartnerAccountResult Succeeded(TenantPartnerAccount account) => new() { Success = true, Account = account };
    public static TenantPartnerAccountResult Failed(string errorCode, string errorMessage) => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of an order context validation operation.
/// </summary>
public record OrderContextValidationResult
{
    public bool IsValid { get; init; }
    public TenantPartnerAccount? Account { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrderContextValidationResult Valid(TenantPartnerAccount account) => new() { IsValid = true, Account = account };
    public static OrderContextValidationResult Invalid(string errorCode, string errorMessage) => new() { IsValid = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
