using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vendorea.PartnerConnect.CustomerPortal.Services;

namespace Vendorea.PartnerConnect.CustomerPortal.Pages.Account;

/// <summary>
/// Public "forgot your password" page. Anonymous by design. Collects an email and POSTs it to the
/// anonymous API, which always returns a generic message (no account enumeration) and — if the
/// account exists — emails a reset link. On submit we always show the same confirmation. Mirrors the
/// Login/Activate pattern (Layout=null, inline CSS, calls the anonymous API directly).
/// </summary>
[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly ApiClient _api;

    public ForgotPasswordModel(ApiClient api) => _api = api;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    /// <summary>Set once submitted; the form is replaced by the generic confirmation.</summary>
    public bool Submitted { get; private set; }

    public string? ConfirmationMessage { get; private set; }

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = "Please enter your email.";
            return Page();
        }

        // Always a generic message, whether or not the email is registered.
        ConfirmationMessage = await _api.ForgotPasswordAsync(Email.Trim());
        Submitted = true;
        return Page();
    }
}
