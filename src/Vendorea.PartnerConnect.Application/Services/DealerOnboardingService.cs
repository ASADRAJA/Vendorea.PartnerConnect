using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing dealer onboarding.
/// </summary>
public interface IDealerOnboardingService
{
    /// <summary>
    /// Submits a new onboarding request.
    /// </summary>
    Task<DealerOnboardingRequest> SubmitRequestAsync(
        DealerOnboardingRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an onboarding request by ID.
    /// </summary>
    Task<DealerOnboardingRequest?> GetRequestAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending onboarding requests.
    /// </summary>
    Task<IReadOnlyList<DealerOnboardingRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an onboarding request and creates the dealer.
    /// </summary>
    Task<ExternalDealer> ApproveRequestAsync(
        Guid requestId,
        string? billingPlanId = null,
        string? reviewNotes = null,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an onboarding request.
    /// </summary>
    Task<DealerOnboardingRequest> RejectRequestAsync(
        Guid requestId,
        string reason,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an external dealer by ID.
    /// </summary>
    Task<ExternalDealer?> GetDealerAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an external dealer by email.
    /// </summary>
    Task<ExternalDealer?> GetDealerByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a dealer's email.
    /// </summary>
    Task<bool> VerifyEmailAsync(int dealerId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a dealer account.
    /// </summary>
    Task<ExternalDealer?> SuspendDealerAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a suspended dealer account.
    /// </summary>
    Task<ExternalDealer?> ActivateDealerAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of dealer onboarding service.
/// </summary>
public class DealerOnboardingService : IDealerOnboardingService
{
    private readonly Interfaces.IOnboardingRepository _onboardingRepository;
    private readonly Interfaces.IExternalDealerRepository _dealerRepository;
    private readonly ILogger<DealerOnboardingService> _logger;

    public DealerOnboardingService(
        Interfaces.IOnboardingRepository onboardingRepository,
        Interfaces.IExternalDealerRepository dealerRepository,
        ILogger<DealerOnboardingService> logger)
    {
        _onboardingRepository = onboardingRepository;
        _dealerRepository = dealerRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DealerOnboardingRequest> SubmitRequestAsync(
        DealerOnboardingRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        request.SubmitterIp = ipAddress;
        request.SubmitterUserAgent = userAgent;
        request.SubmittedAt = DateTime.UtcNow;
        request.Status = OnboardingStatus.Submitted;

        await _onboardingRepository.AddAsync(request, cancellationToken);

        _logger.LogInformation(
            "New onboarding request {RequestId} submitted for {CompanyName}",
            request.Id, request.CompanyName);

        return request;
    }

    /// <inheritdoc />
    public Task<DealerOnboardingRequest?> GetRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _onboardingRepository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DealerOnboardingRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        return _onboardingRepository.GetPendingAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ExternalDealer> ApproveRequestAsync(
        Guid requestId,
        string? billingPlanId = null,
        string? reviewNotes = null,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _onboardingRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Onboarding request {requestId} not found");
        }

        if (request.Status == OnboardingStatus.Approved)
        {
            throw new InvalidOperationException("Request has already been approved");
        }

        // Create the dealer
        var verificationToken = GenerateToken();
        var dealer = new ExternalDealer
        {
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            PrimaryContactName = request.PrimaryContactName,
            PrimaryContactEmail = request.PrimaryContactEmail,
            BillingPlanId = billingPlanId,
            Status = ExternalDealerStatus.Pending, // Will activate after email verification
            VerificationToken = verificationToken,
            VerificationTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await _dealerRepository.AddAsync(dealer, cancellationToken);

        // Update the request
        request.Status = OnboardingStatus.Approved;
        request.ReviewedBy = reviewedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = reviewNotes;
        request.DealerId = dealer.Id;

        await _onboardingRepository.UpdateAsync(request, cancellationToken);

        _logger.LogInformation(
            "Onboarding request {RequestId} approved, created dealer {DealerId}",
            requestId, dealer.Id);

        // TODO: Send verification email

        return dealer;
    }

    /// <inheritdoc />
    public async Task<DealerOnboardingRequest> RejectRequestAsync(
        Guid requestId,
        string reason,
        string? reviewedBy = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _onboardingRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Onboarding request {requestId} not found");
        }

        request.Status = OnboardingStatus.Rejected;
        request.ReviewedBy = reviewedBy;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = reason;

        await _onboardingRepository.UpdateAsync(request, cancellationToken);

        _logger.LogInformation(
            "Onboarding request {RequestId} rejected: {Reason}",
            requestId, reason);

        return request;
    }

    /// <inheritdoc />
    public Task<ExternalDealer?> GetDealerAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dealerRepository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExternalDealer?> GetDealerByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _dealerRepository.GetByEmailAsync(email, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyEmailAsync(int dealerId, string token, CancellationToken cancellationToken = default)
    {
        var dealer = await _dealerRepository.GetByIdAsync(dealerId, cancellationToken);
        if (dealer == null) return false;

        if (dealer.IsEmailVerified) return true;

        if (dealer.VerificationToken != token)
        {
            _logger.LogWarning("Invalid verification token for dealer {DealerId}", dealerId);
            return false;
        }

        if (dealer.VerificationTokenExpiresAt.HasValue && dealer.VerificationTokenExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired verification token for dealer {DealerId}", dealerId);
            return false;
        }

        dealer.IsEmailVerified = true;
        dealer.EmailVerifiedAt = DateTime.UtcNow;
        dealer.VerificationToken = null;
        dealer.VerificationTokenExpiresAt = null;
        dealer.Status = ExternalDealerStatus.Active;
        dealer.ActivatedAt = DateTime.UtcNow;
        dealer.UpdatedAt = DateTime.UtcNow;

        await _dealerRepository.UpdateAsync(dealer, cancellationToken);

        _logger.LogInformation("Email verified for dealer {DealerId}", dealerId);

        return true;
    }

    /// <inheritdoc />
    public async Task<ExternalDealer?> SuspendDealerAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var dealer = await _dealerRepository.GetByIdAsync(id, cancellationToken);
        if (dealer == null) return null;

        dealer.Status = ExternalDealerStatus.Suspended;
        dealer.SuspendedAt = DateTime.UtcNow;
        dealer.SuspensionReason = reason;
        dealer.UpdatedAt = DateTime.UtcNow;

        await _dealerRepository.UpdateAsync(dealer, cancellationToken);

        _logger.LogInformation("Dealer {DealerId} suspended: {Reason}", id, reason);

        return dealer;
    }

    /// <inheritdoc />
    public async Task<ExternalDealer?> ActivateDealerAsync(int id, CancellationToken cancellationToken = default)
    {
        var dealer = await _dealerRepository.GetByIdAsync(id, cancellationToken);
        if (dealer == null) return null;

        dealer.Status = ExternalDealerStatus.Active;
        dealer.SuspendedAt = null;
        dealer.SuspensionReason = null;
        dealer.ActivatedAt = DateTime.UtcNow;
        dealer.UpdatedAt = DateTime.UtcNow;

        await _dealerRepository.UpdateAsync(dealer, cancellationToken);

        _logger.LogInformation("Dealer {DealerId} activated", id);

        return dealer;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
