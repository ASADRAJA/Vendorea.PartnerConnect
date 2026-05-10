using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Controller for dealer onboarding.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly IDealerOnboardingService _onboardingService;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(
        IDealerOnboardingService onboardingService,
        ILogger<OnboardingController> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a new onboarding request.
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> SubmitRequest(
        [FromBody] OnboardingRequestDto request,
        CancellationToken cancellationToken)
    {
        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers["User-Agent"].FirstOrDefault();

        var onboardingRequest = new DealerOnboardingRequest
        {
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country ?? "US",
            PrimaryContactName = request.PrimaryContactName,
            PrimaryContactEmail = request.PrimaryContactEmail,
            RequestedPlan = request.RequestedPlan,
            Notes = request.Notes
        };

        var result = await _onboardingService.SubmitRequestAsync(
            onboardingRequest,
            ipAddress,
            userAgent,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetRequest),
            new { id = result.Id },
            new { id = result.Id, status = result.Status.ToString() });
    }

    /// <summary>
    /// Gets the status of an onboarding request.
    /// </summary>
    [HttpGet("request/{id:guid}")]
    public async Task<IActionResult> GetRequest(Guid id, CancellationToken cancellationToken)
    {
        var request = await _onboardingService.GetRequestAsync(id, cancellationToken);

        if (request == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            request.Id,
            request.CompanyName,
            request.Email,
            Status = request.Status.ToString(),
            request.SubmittedAt,
            request.ReviewedAt,
            request.ReviewNotes
        });
    }

    /// <summary>
    /// Gets pending onboarding requests (admin).
    /// </summary>
    [HttpGet("requests/pending")]
    public async Task<IActionResult> GetPendingRequests(CancellationToken cancellationToken)
    {
        var requests = await _onboardingService.GetPendingRequestsAsync(cancellationToken);

        return Ok(requests.Select(r => new
        {
            r.Id,
            r.CompanyName,
            r.Email,
            r.PrimaryContactName,
            Status = r.Status.ToString(),
            r.RequestedPlan,
            r.SubmittedAt
        }));
    }

    /// <summary>
    /// Approves an onboarding request (admin).
    /// </summary>
    [HttpPost("request/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(
        Guid id,
        [FromBody] ApproveRequestDto? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dealer = await _onboardingService.ApproveRequestAsync(
                id,
                request?.BillingPlanId,
                request?.Notes,
                User.Identity?.Name,
                cancellationToken);

            return Ok(new
            {
                dealer.Id,
                dealer.CompanyName,
                dealer.Email,
                Status = dealer.Status.ToString(),
                Message = "Onboarding approved. Verification email sent."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Rejects an onboarding request (admin).
    /// </summary>
    [HttpPost("request/{id:guid}/reject")]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromBody] RejectRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _onboardingService.RejectRequestAsync(
                id,
                request.Reason,
                User.Identity?.Name,
                cancellationToken);

            return Ok(new
            {
                result.Id,
                Status = result.Status.ToString(),
                result.ReviewNotes
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Verifies a dealer's email.
    /// </summary>
    [HttpGet("verify")]
    public async Task<IActionResult> VerifyEmail(
        [FromQuery] int dealerId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var verified = await _onboardingService.VerifyEmailAsync(dealerId, token, cancellationToken);

        if (!verified)
        {
            return BadRequest("Invalid or expired verification token");
        }

        return Ok(new { message = "Email verified successfully. Your account is now active." });
    }

    private string? GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public record OnboardingRequestDto
{
    public required string CompanyName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? PrimaryContactName { get; init; }
    public string? PrimaryContactEmail { get; init; }
    public string? RequestedPlan { get; init; }
    public string? Notes { get; init; }
}

public record ApproveRequestDto
{
    public string? BillingPlanId { get; init; }
    public string? Notes { get; init; }
}

public record RejectRequestDto
{
    public required string Reason { get; init; }
}
