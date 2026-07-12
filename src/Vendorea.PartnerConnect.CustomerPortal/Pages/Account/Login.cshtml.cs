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
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Invalid email or password.";
            return Page();
        }

        // Exchange the email/password for a per-user token via the org auth endpoint.
        var login = await _api.LoginAsync(Email.Trim(), Password);
        if (login is null || string.IsNullOrEmpty(login.Token))
        {
            Error = "Invalid email or password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.User.Id),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(login.User.DisplayName) ? login.User.Email : login.User.DisplayName),
            new(ClaimTypes.Email, login.User.Email),
            new("org_id", login.Organization.Id.ToString()),
            new("org_name", login.Organization.Name),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(login.User.Role) ? "Viewer" : login.User.Role),
            // The per-user API token — the ApiClient attaches it as Authorization: Bearer per request.
            new(ApiClient.TokenClaim, login.Token)
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
