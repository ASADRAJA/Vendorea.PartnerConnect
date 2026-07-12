using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vendorea.PartnerConnect.CustomerPortal.Services;

namespace Vendorea.PartnerConnect.CustomerPortal.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ApiClient _api;

    public LoginModel(ApiClient api) => _api = api;

    [BindProperty]
    public string ApiKey { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Error = "Enter your organization API key.";
            return Page();
        }

        var key = ApiKey.Trim();

        // Validate the key by calling GET /org/me with it. A 200 means the key resolves to an
        // active organization.
        var context = await _api.ValidateKeyAsync(key);
        if (context is null)
        {
            Error = "Invalid organization API key.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.Organization.Id.ToString()),
            new(ClaimTypes.Name, context.Organization.Name),
            new("org_id", context.Organization.Id.ToString()),
            new("org_name", context.Organization.Name),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(context.User.Role) ? "OrgAdmin" : context.User.Role),
            // Dev-grade for the MVP: the org API key is stored directly in a cookie claim so the
            // ApiClient can attach it per request. This should move to a protected server-side
            // session (or SSO token) before production — see docs/customer-portal spec.
            new(ApiClient.ApiKeyClaim, key)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);
        return LocalRedirect("/");
    }
}
