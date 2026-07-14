using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vendorea.PartnerConnect.CustomerPortal.Services;

namespace Vendorea.PartnerConnect.CustomerPortal.Pages.Account;

/// <summary>
/// Public "set a new password" page for a forgotten-password link. GET validates the reset token (via
/// the anonymous API) to show whose account it is; POST sets the new password and redirects to login.
/// Anonymous by design — it must not require the auth cookie/bearer token. Mirrors the Activate page.
/// </summary>
[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private const int MinPasswordLength = 8;

    private readonly ApiClient _api;

    public ResetPasswordModel(ApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>True when the token is valid and the set-password form should be shown.</summary>
    public bool TokenValid { get; private set; }

    public string? Email { get; private set; }
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        await LoadTokenInfoAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Re-validate the token so we can re-render context if anything below fails.
        await LoadTokenInfoAsync();
        if (!TokenValid)
            return Page();

        if (string.IsNullOrEmpty(Password) || Password.Length < MinPasswordLength)
        {
            Error = $"Password must be at least {MinPasswordLength} characters.";
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            Error = "Passwords don't match.";
            return Page();
        }

        var result = await _api.ResetPasswordAsync(Token!, Password);
        if (!result.Success)
        {
            Error = result.Error ?? "Couldn't reset your password. Please try again.";
            return Page();
        }

        return RedirectToPage("/Account/Login", new { message = "Your password has been reset — please sign in." });
    }

    private async Task LoadTokenInfoAsync()
    {
        TokenValid = false;
        if (string.IsNullOrWhiteSpace(Token))
        {
            Error = "This password reset link is invalid or has expired. Please request a new one.";
            return;
        }

        var result = await _api.GetResetPasswordInfoAsync(Token);
        if (!result.Success || result.Info is null)
        {
            Error = result.Error ?? "This password reset link is invalid or has expired. Please request a new one.";
            return;
        }

        TokenValid = true;
        Email = result.Info.Email;
    }
}
