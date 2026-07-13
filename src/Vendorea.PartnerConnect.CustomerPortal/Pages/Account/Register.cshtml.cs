using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vendorea.PartnerConnect.CustomerPortal.Models;
using Vendorea.PartnerConnect.CustomerPortal.Services;

namespace Vendorea.PartnerConnect.CustomerPortal.Pages.Account;

/// <summary>
/// Public "Register your organization" page. Anonymous by design — it collects the org name, chosen
/// plan, and the intended first admin's details and POSTs them to the anonymous public API (no auth
/// cookie/bearer token). On success it shows a "pending review" confirmation; a PC operator approves
/// it later, which triggers the admin's activation email. Mirrors the Login/Activate pattern
/// (Layout=null, inline CSS, calls the anonymous API directly).
/// </summary>
[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly ApiClient _api;

    public RegisterModel(ApiClient api) => _api = api;

    [BindProperty]
    public string OrganizationName { get; set; } = string.Empty;

    [BindProperty]
    public string PlanCode { get; set; } = string.Empty;

    [BindProperty]
    public string AdminDisplayName { get; set; } = string.Empty;

    [BindProperty]
    public string AdminEmail { get; set; } = string.Empty;

    [BindProperty]
    public string? ContactPhone { get; set; }

    /// <summary>Plans offered in the selector (loaded from the anonymous public API).</summary>
    public List<PublicPlanDto> Plans { get; private set; } = new();

    public string? Error { get; set; }

    /// <summary>Set once the registration was accepted; the form is replaced by a thank-you note.</summary>
    public bool Submitted { get; private set; }

    public string? ConfirmationMessage { get; private set; }

    public async Task OnGetAsync()
    {
        Plans = await _api.GetPublicPlansAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Plans = await _api.GetPublicPlansAsync();

        if (string.IsNullOrWhiteSpace(OrganizationName))
        {
            Error = "Please enter your organization name.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(PlanCode))
        {
            Error = "Please choose a plan.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(AdminDisplayName))
        {
            Error = "Please enter the administrator's name.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(AdminEmail))
        {
            Error = "Please enter the administrator's email.";
            return Page();
        }

        var result = await _api.RegisterOrganizationAsync(
            OrganizationName.Trim(), PlanCode.Trim(), AdminDisplayName.Trim(), AdminEmail.Trim(),
            string.IsNullOrWhiteSpace(ContactPhone) ? null : ContactPhone.Trim());

        if (!result.Success)
        {
            Error = result.Error ?? "We couldn't submit your registration. Please try again.";
            return Page();
        }

        Submitted = true;
        ConfirmationMessage = result.Message;
        return Page();
    }
}
