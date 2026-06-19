using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authentication;

/// <summary>
/// Authentication handler for API key authentication. Resolves, in order: the development-only
/// dev-admin key, an organization key (e.g. Merchant360's portal key), then a dealer-scoped
/// API key. Each produces a principal carrying <c>scope</c> claims used by
/// <see cref="ScopeRequirement"/> authorization.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeyService;
    private readonly IOrgApiKeyAuthenticator _orgAuthenticator;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    public const string AuthenticationScheme = "ApiKey";
    public const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService,
        IOrgApiKeyAuthenticator orgAuthenticator,
        IConfiguration configuration,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
        _orgAuthenticator = orgAuthenticator;
        _configuration = configuration;
        _environment = environment;
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

        // Dev admin key — DEVELOPMENT ONLY. Never honored outside Development even if configured.
        var devAdminKey = _configuration["DevAdminKey"];
        if (_environment.IsDevelopment()
            && !string.IsNullOrEmpty(devAdminKey)
            && providedApiKey == devAdminKey)
        {
            Logger.LogDebug("Dev admin key authentication successful");
            return AuthenticateResult.Success(CreateDevAdminTicket());
        }

        // Organization key (e.g. Merchant360). Returns the org only for an active organization.
        var organization = await _orgAuthenticator.ResolveActiveOrganizationAsync(providedApiKey, Context.RequestAborted);
        if (organization != null)
        {
            Context.Items["Organization"] = organization;
            return AuthenticateResult.Success(CreateOrganizationTicket(organization));
        }

        // Get client IP
        var clientIp = GetClientIpAddress();

        // Validate as a dealer-scoped API key
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

    private static AuthenticationTicket CreateOrganizationTicket(Organization organization)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"org:{organization.Id}"),
            new(ClaimTypes.Name, organization.Name ?? $"Org {organization.Id}"),
            new(ApiPrincipalExtensions.ActorTypeClaim, ApiPrincipalExtensions.OrganizationActor),
            new(ApiPrincipalExtensions.OrgIdClaim, organization.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(organization.Code))
            claims.Add(new Claim("org_code", organization.Code));

        foreach (var scope in ApiScopes.OrganizationDefault)
            claims.Add(new Claim(ScopeAuthorizationHandler.ScopeClaimType, scope));

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme);
    }

    private AuthenticationTicket CreateDevAdminTicket()
    {
        // Create a dev admin principal with full permissions
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "0"),
            new(ClaimTypes.Name, "DevAdmin"),
            new("ApiKeyId", Guid.Empty.ToString()),
            new("DealerId", "0"),
            new("KeyPrefix", "dev-admin"),
            new("scope", ApiScopes.All)
        };

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, AuthenticationScheme);
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
