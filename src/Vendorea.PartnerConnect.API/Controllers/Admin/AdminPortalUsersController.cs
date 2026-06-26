using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Persistence;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Manages Admin Portal login users (username/password + portal role). The portal calls these
/// endpoints with the shared admin API key; per-user role enforcement happens in the portal UI.
/// </summary>
[ApiController]
[Route("api/admin/portal-users")]
public class AdminPortalUsersController : ControllerBase
{
    private readonly PartnerConnectDbContext _db;
    private readonly ILogger<AdminPortalUsersController> _logger;

    public AdminPortalUsersController(PartnerConnectDbContext db, ILogger<AdminPortalUsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Validates a username/password and returns the user (used by the portal login page).</summary>
    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthenticatePortalUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Username and password are required." });

        var user = await _db.AdminPortalUsers
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !user.IsActive || !PortalPasswordHasher.Verify(user.PasswordHash, request.Password))
            return Unauthorized(new { error = "Invalid username or password." });

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(user));
    }

    /// <summary>Lists all portal users.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _db.AdminPortalUsers
            .OrderBy(u => u.Username)
            .ToListAsync(ct);
        return Ok(users.Select(ToDto));
    }

    /// <summary>Creates a new portal user.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortalUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request.Role}'." });

        var username = request.Username.Trim();
        if (await _db.AdminPortalUsers.AnyAsync(u => u.Username == username, ct))
            return Conflict(new { error = $"A user named '{username}' already exists." });

        var user = new AdminPortalUser
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            Role = role,
            IsActive = true,
            PasswordHash = PortalPasswordHasher.Hash(request.Password)
        };

        _db.AdminPortalUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created admin portal user {Username} ({Role})", user.Username, user.Role);
        return Ok(ToDto(user));
    }

    /// <summary>Updates a user's role, display name, and active state.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePortalUserRequest request, CancellationToken ct)
    {
        var user = await _db.AdminPortalUsers.FindAsync(new object?[] { id }, ct);
        if (user is null)
            return NotFound();

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = $"Invalid role '{request.Role}'." });

        // Guard: don't allow removing the last active Admin (lock-out protection).
        if (user.Role == AdminPortalRole.Admin && (role != AdminPortalRole.Admin || !request.IsActive))
        {
            var otherActiveAdmins = await _db.AdminPortalUsers
                .CountAsync(u => u.Id != id && u.Role == AdminPortalRole.Admin && u.IsActive, ct);
            if (otherActiveAdmins == 0)
                return BadRequest(new { error = "Cannot demote or deactivate the last active Admin." });
        }

        user.Role = role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(user));
    }

    /// <summary>Resets a user's password.</summary>
    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPortalUserPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "New password is required." });

        var user = await _db.AdminPortalUsers.FindAsync(new object?[] { id }, ct);
        if (user is null)
            return NotFound();

        user.PasswordHash = PortalPasswordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Deletes a portal user.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _db.AdminPortalUsers.FindAsync(new object?[] { id }, ct);
        if (user is null)
            return NotFound();

        if (user.Role == AdminPortalRole.Admin)
        {
            var otherActiveAdmins = await _db.AdminPortalUsers
                .CountAsync(u => u.Id != id && u.Role == AdminPortalRole.Admin && u.IsActive, ct);
            if (otherActiveAdmins == 0)
                return BadRequest(new { error = "Cannot delete the last active Admin." });
        }

        _db.AdminPortalUsers.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool TryParseRole(string? value, out AdminPortalRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);

    private static PortalUserDto ToDto(AdminPortalUser u) => new(
        u.Id, u.Username, u.DisplayName, u.Role.ToString(), u.IsActive, u.CreatedAt, u.LastLoginAt);
}

public record AuthenticatePortalUserRequest(string Username, string Password);
public record CreatePortalUserRequest(string Username, string Password, string Role, string? DisplayName);
public record UpdatePortalUserRequest(string Role, bool IsActive, string? DisplayName);
public record ResetPortalUserPasswordRequest(string NewPassword);
public record PortalUserDto(
    Guid Id, string Username, string DisplayName, string Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);
