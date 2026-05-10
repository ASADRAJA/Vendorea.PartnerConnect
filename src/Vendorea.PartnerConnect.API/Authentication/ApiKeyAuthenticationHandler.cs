using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authentication;

/// <summary>
/// Authentication handler for API key authentication.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeyService;
    public const string AuthenticationScheme = "ApiKey";
    public const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key in header
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        // Get client IP
        var clientIp = GetClientIpAddress();

        // Validate the API key
        var validationResult = await _apiKeyService.ValidateAsync(providedApiKey, clientIp);

        if (!validationResult.IsValid)
        {
            Logger.LogWarning("API key authentication failed: {Error}", validationResult.ErrorMessage);
            return AuthenticateResult.Fail(validationResult.ErrorMessage ?? "Invalid API key");
        }

        var apiKey = validationResult.ApiKey!;

        // Create claims principal
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.DealerId.ToString()),
            new(ClaimTypes.Name, apiKey.Name),
            new("ApiKeyId", apiKey.Id.ToString()),
            new("DealerId", apiKey.DealerId.ToString()),
            new("KeyPrefix", apiKey.KeyPrefix)
        };

        // Add scope claims
        foreach (var scope in apiKey.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        // Store API key in context for later use
        Context.Items["ApiKey"] = apiKey;

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.Append("WWW-Authenticate", $"ApiKey realm=\"{Options.Realm}\"");
        return base.HandleChallengeAsync(properties);
    }

    private string? GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return Context.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Realm { get; set; } = "Vendorea PartnerConnect";
}

/// <summary>
/// Extension methods for configuring API key authentication.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.AuthenticationScheme,
            configureOptions ?? (_ => { }));
    }

    /// <summary>
    /// Gets the API key from the current HTTP context.
    /// </summary>
    public static ApiKey? GetApiKey(this HttpContext context)
    {
        return context.Items["ApiKey"] as ApiKey;
    }

    /// <summary>
    /// Gets the dealer ID from the current authenticated user.
    /// </summary>
    public static int? GetDealerId(this ClaimsPrincipal user)
    {
        var dealerIdClaim = user.FindFirst("DealerId");
        if (dealerIdClaim != null && int.TryParse(dealerIdClaim.Value, out var dealerId))
        {
            return dealerId;
        }
        return null;
    }

    /// <summary>
    /// Checks if the user has a specific scope.
    /// </summary>
    public static bool HasScope(this ClaimsPrincipal user, string scope)
    {
        var scopes = user.FindAll("scope").Select(c => c.Value);
        return scopes.Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || s.Equals(ApiScopes.All, StringComparison.Ordinal));
    }
}
