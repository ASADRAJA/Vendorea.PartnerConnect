using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.FluentUI.AspNetCore.Components;
using Vendorea.PartnerConnect.CustomerPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server + Razor Pages (login/logout must run outside a live Blazor circuit).
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

// Fluent UI Blazor.
builder.Services.AddFluentUIComponents();

// Per-circuit portal state (org context + selected tenant).
builder.Services.AddScoped<PortalState>();

// Cookie authentication. The org portal API key is exchanged for a cookie via the
// /Account/Login Razor Page; /account/logout clears it.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "PartnerConnectCustomer.Auth";
    });

builder.Services.AddAuthorization();

// Typed HttpClient for the org-facing API. The X-Api-Key is attached per-request from the
// current user's org_api_key claim (see ApiClient), not baked in here.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapRazorPages();

// Logout: clear the auth cookie and return to the login page.
app.MapGet("/account/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Account/Login");
});

// Everything else renders the Blazor host, which requires an authenticated user.
app.MapFallbackToPage("/_Host").RequireAuthorization();

app.Run();
