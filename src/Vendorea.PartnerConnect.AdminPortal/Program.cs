using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vendorea.PartnerConnect.AdminPortal.Services;

var builder = WebApplication.CreateBuilder(args);

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
