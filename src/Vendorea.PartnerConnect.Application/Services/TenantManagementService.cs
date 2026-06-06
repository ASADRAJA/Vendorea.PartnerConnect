using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

public class TenantManagementService : ITenantManagementService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantPartnerAccountRepository _accountRepository;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        IOrganizationRepository organizationRepository,
        ITenantRepository tenantRepository,
        ITenantPartnerAccountRepository accountRepository,
        ITradingPartnerRepository tradingPartnerRepository,
        ILogger<TenantManagementService> logger)
    {
        _organizationRepository = organizationRepository;
        _tenantRepository = tenantRepository;
        _accountRepository = accountRepository;
        _tradingPartnerRepository = tradingPartnerRepository;
        _logger = logger;
    }

    public async Task<OrganizationResult> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        // Validate code uniqueness
        if (await _organizationRepository.CodeExistsAsync(request.Code, cancellationToken))
        {
            return OrganizationResult.Failed("ORG_CODE_EXISTS", $"Organization code '{request.Code}' already exists");
        }

        var organization = new Organization
        {
            Code = request.Code,
            Name = request.Name,
            Status = OrganizationStatus.Pending,
            IsMultiTenant = request.IsMultiTenant,
            BillingPlanId = request.BillingPlanId,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            CreatedAt = DateTime.UtcNow
        };

        organization = await _organizationRepository.AddAsync(organization, cancellationToken);

        // For single-tenant orgs, create the default tenant automatically
        if (!request.IsMultiTenant)
        {
            var defaultTenant = new Tenant
            {
                OrganizationId = organization.Id,
                Code = "DEFAULT",
                Name = organization.Name,
                Status = TenantStatus.Active,
                IsDefault = true,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                CreatedAt = DateTime.UtcNow
            };

            await _tenantRepository.AddAsync(defaultTenant, cancellationToken);
        }

        _logger.LogInformation("Created organization {OrgCode} (ID: {OrgId}, MultiTenant: {IsMultiTenant})",
            organization.Code, organization.Id, organization.IsMultiTenant);

        return OrganizationResult.Succeeded(organization);
    }

    public async Task<Organization?> GetOrganizationAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _organizationRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Organization?> GetOrganizationByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _organizationRepository.GetByCodeAsync(code, cancellationToken);
    }

    public async Task<OrganizationResult> UpdateOrganizationStatusAsync(int id, OrganizationStatus status, string? reason = null, CancellationToken cancellationToken = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (organization == null)
        {
            return OrganizationResult.Failed("ORG_NOT_FOUND", $"Organization with ID {id} not found");
        }

        var previousStatus = organization.Status;
        organization.Status = status;
        organization.UpdatedAt = DateTime.UtcNow;

        switch (status)
        {
            case OrganizationStatus.Active:
                organization.ActivatedAt = DateTime.UtcNow;
                organization.SuspendedAt = null;
                organization.SuspensionReason = null;
                break;
            case OrganizationStatus.Suspended:
                organization.SuspendedAt = DateTime.UtcNow;
                organization.SuspensionReason = reason;
                break;
        }

        await _organizationRepository.UpdateAsync(organization, cancellationToken);

        _logger.LogInformation("Updated organization {OrgId} status from {PreviousStatus} to {NewStatus}",
            id, previousStatus, status);

        return OrganizationResult.Succeeded(organization);
    }

    public async Task<TenantResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Validate organization exists
        var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken);
        if (organization == null)
        {
            return TenantResult.Failed("ORG_NOT_FOUND", $"Organization with ID {request.OrganizationId} not found");
        }

        // Validate code uniqueness within organization
        var existingTenant = await _tenantRepository.GetByCodeAsync(request.OrganizationId, request.Code, cancellationToken);
        if (existingTenant != null)
        {
            return TenantResult.Failed("TENANT_CODE_EXISTS", $"Tenant code '{request.Code}' already exists in this organization");
        }

        var tenant = new Tenant
        {
            OrganizationId = request.OrganizationId,
            Code = request.Code,
            Name = request.Name,
            Status = TenantStatus.Active,
            IsDefault = request.IsDefault,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            ExternalId = request.ExternalId,
            CreatedAt = DateTime.UtcNow
        };

        tenant = await _tenantRepository.AddAsync(tenant, cancellationToken);

        _logger.LogInformation("Created tenant {TenantCode} (ID: {TenantId}) for organization {OrgId}",
            tenant.Code, tenant.Id, request.OrganizationId);

        return TenantResult.Succeeded(tenant);
    }

    public async Task<Tenant?> GetTenantAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetTenantsAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetByOrganizationIdAsync(organizationId, cancellationToken);
    }

    public async Task<TenantPartnerAccountResult> LinkPartnerAccountAsync(LinkPartnerAccountRequest request, CancellationToken cancellationToken = default)
    {
        // Validate tenant exists
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return TenantPartnerAccountResult.Failed("TENANT_NOT_FOUND", $"Tenant with ID {request.TenantId} not found");
        }

        // Validate trading partner exists
        var partner = await _tradingPartnerRepository.GetByIdAsync(request.TradingPartnerId, cancellationToken);
        if (partner == null)
        {
            return TenantPartnerAccountResult.Failed("PARTNER_NOT_FOUND", $"Trading partner with ID {request.TradingPartnerId} not found");
        }

        // Check for duplicate
        if (await _accountRepository.ExistsAsync(request.TenantId, request.TradingPartnerId, request.AccountNumber, cancellationToken))
        {
            return TenantPartnerAccountResult.Failed("ACCOUNT_EXISTS", "This account is already linked to the tenant");
        }

        var account = new TenantPartnerAccount
        {
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            AccountNumber = request.AccountNumber,
            DisplayName = request.DisplayName,
            IsDefault = request.IsDefault,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        account = await _accountRepository.AddAsync(account, cancellationToken);

        _logger.LogInformation("Linked account {AccountNumber} for tenant {TenantId} with partner {PartnerId}",
            request.AccountNumber, request.TenantId, request.TradingPartnerId);

        return TenantPartnerAccountResult.Succeeded(account);
    }

    public async Task<IReadOnlyList<TenantPartnerAccount>> GetPartnerAccountsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        return await _accountRepository.GetByTenantIdAsync(tenantId, cancellationToken);
    }

    public async Task<OrderContextValidationResult> ValidateOrderContextAsync(
        int organizationId,
        int tenantId,
        int tradingPartnerId,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate organization exists and is active
        var organization = await _organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        if (organization == null)
        {
            return OrderContextValidationResult.Invalid("ORG_NOT_FOUND", $"Organization with ID {organizationId} not found");
        }

        if (organization.Status != OrganizationStatus.Active)
        {
            return OrderContextValidationResult.Invalid("ORG_NOT_ACTIVE", $"Organization is not active (status: {organization.Status})");
        }

        // 2. Validate tenant exists and belongs to organization
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            return OrderContextValidationResult.Invalid("TENANT_NOT_FOUND", $"Tenant with ID {tenantId} not found");
        }

        if (tenant.OrganizationId != organizationId)
        {
            return OrderContextValidationResult.Invalid("TENANT_NOT_IN_ORG", "Tenant does not belong to the specified organization");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return OrderContextValidationResult.Invalid("TENANT_NOT_ACTIVE", $"Tenant is not active (status: {tenant.Status})");
        }

        // 3. Validate tenant partner account exists and is active
        var account = await _accountRepository.GetByTenantPartnerAccountAsync(tenantId, tradingPartnerId, accountNumber, cancellationToken);
        if (account == null)
        {
            return OrderContextValidationResult.Invalid("ACCOUNT_NOT_FOUND", $"No active account found for tenant {tenantId} with partner {tradingPartnerId} and account number '{accountNumber}'");
        }

        return OrderContextValidationResult.Valid(account);
    }
}
