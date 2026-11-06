using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Api.RateLimiting;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// The provider's redirect target. Anonymous by necessity — it is reached by a browser carrying
/// nothing of ours but the state token.
/// </summary>
/// <remarks>
/// Being anonymous is the whole reason the state token exists. This endpoint trusts exactly one
/// thing in the request: that a state hashing to a stored, unused, unexpired row was presented.
/// Organization, tenant, connection and destination all come from that row. Nothing in the query
/// string decides what gets attached to whom.
///
/// Rate limited like the other public doors, so the state space cannot be swept.
/// </remarks>
[ApiController]
[Route("api/v1/integrations")]
[AllowAnonymous]
public class IntegrationCallbackController : ControllerBase
{
    private readonly IIntegrationAuthorizationService _authorization;
    private readonly IntegrationAuthorizationOptions _options;
    private readonly ILogger<IntegrationCallbackController> _logger;

    public IntegrationCallbackController(
        IIntegrationAuthorizationService authorization,
        IOptions<IntegrationAuthorizationOptions> options,
        ILogger<IntegrationCallbackController> logger)
    {
        _authorization = authorization;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The provider redirects here after the user consents.
    /// </summary>
    /// <remarks>
    /// The query parameter names are Amazon's and must stay exactly as they are: this URL is
    /// registered on the Amazon application, and Amazon chooses the query string. Other providers
    /// will add their own optional parameters rather than renaming these.
    ///
    /// <c>providerCode</c> in the route is checked against the connection the state names, so a
    /// state cannot be redeemed through a different provider's door.
    /// </remarks>
    [HttpGet("callback/{providerCode}")]
    [EnableRateLimiting(RateLimitPolicies.PublicAuth)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback(
        string providerCode,
        [FromQuery] string? state,
        [FromQuery(Name = "spapi_oauth_code")] string? spapiOauthCode,
        [FromQuery(Name = "selling_partner_id")] string? sellingPartnerId,
        CancellationToken cancellationToken)
    {
        var result = await _authorization.CompleteAsync(
            providerCode, state, spapiOauthCode, sellingPartnerId, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "{ProviderCode} authorization callback rejected: {Failure}",
                providerCode, result.Failure);

            // The browser gets one message for every failure. Distinguishing "unknown state" from
            // "expired state" here would tell a prober which guesses were close.
            return Respond(
                returnUrl: null,
                success: false,
                message: "Authorization could not be completed. Please start again from Merchant360.");
        }

        return Respond(
            returnUrl: result.ReturnUrl,
            success: true,
            message: result.ReusedExisting
                ? "Your Amazon account has been reconnected."
                : "Your Amazon account is connected.");
    }

    /// <summary>
    /// Redirects to the stored return URL when it is still allowed; otherwise renders a plain page.
    /// </summary>
    /// <remarks>
    /// The allow-list is checked again here, not only when the state was issued. A URL that was
    /// permitted ten minutes ago may have been removed from the configuration since, and the
    /// check that matters is the one made at the moment of redirecting.
    /// </remarks>
    private IActionResult Respond(string? returnUrl, bool success, string message)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && _options.IsReturnUrlAllowed(returnUrl))
        {
            var separator = returnUrl!.Contains('?') ? '&' : '?';
            return Redirect($"{returnUrl}{separator}integrationStatus={(success ? "connected" : "failed")}");
        }

        // HtmlEncode even though both messages are constants: this method is one careless edit away
        // from carrying a provider-supplied string into markup.
        var body = $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <title>Amazon authorization</title></head>
            <body style="font-family:system-ui,sans-serif;margin:3rem;">
            <h1>{(success ? "Connected" : "Not connected")}</h1>
            <p>{WebUtility.HtmlEncode(message)}</p>
            <p>You may close this window.</p>
            </body></html>
            """;

        return new ContentResult
        {
            Content = body,
            ContentType = "text/html; charset=utf-8",
            StatusCode = success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }
}
