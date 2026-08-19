using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vendorea.PartnerConnect.AdminPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// Pin the culture before anything formats a value. The host's locale would otherwise decide it:
// Ubuntu server defaults to LANG=C.UTF-8, which .NET maps to the invariant culture, whose
// currency symbol is empty - money then renders with the generic sign rather than a dollar sign.
// DefaultThreadCurrentCulture rather than RequestLocalization so background work is covered too.
{
    var cultureName = builder.Configuration["Localization:DefaultCulture"];
    System.Globalization.CultureInfo culture;
    try
    {
        culture = new System.Globalization.CultureInfo(
            string.IsNullOrWhiteSpace(cultureName) ? "en-US" : cultureName);
    }
    catch (System.Globalization.CultureNotFoundException)
    {
        // A typo must not drop the process back to invariant and reintroduce the bug.
        culture = new System.Globalization.CultureInfo("en-US");
    }

    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
    System.Globalization.CultureInfo.CurrentCulture = culture;
    System.Globalization.CultureInfo.CurrentUICulture = culture;
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

// Cookie authentication. Login/logout happen via the /Account/Login Razor Page + /account/logout
// endpoint (cookies can't be issued from inside a live Blazor circuit).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "PartnerConnectAdmin.Auth";
    });

// Portal authorization policies (role claim = "Admin" | "Support" | "ReadOnly").
builder.Services.AddAuthorization(options =>
{
    // Config edits, power tools, user management.
    options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
    // Approvals / runs / retries / activate-suspend (Admin or Support).
    options.AddPolicy("RequireOperator", p => p.RequireRole("Admin", "Support"));
});

// Configure HttpClient for API calls
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMinutes(10); // Increased for large file uploads

    // Add API key for authentication
    var apiKey = builder.Configuration["ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
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
