using ConnectHub.Auth.Models.DTOs;
using ConnectHub.Auth.Models.Entities;
using ConnectHub.Auth.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace ConnectHub.Auth.Controllers;

/// <summary>
/// UserController — ConnectHub Auth Controllers
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;
    private readonly IConfiguration _configuration;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger,
        IConfiguration configuration)
    {
        _userService = userService;
        _logger = logger;
        _configuration = configuration;
    }

    // ── Register ────────────────────────────────────────────────

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<UserProfileDto>), 201)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 400)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 409)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed",
                ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()));

        try
        {
            var user = new User
            {
                UserName    = dto.UserName.Trim(),
                DisplayName = dto.DisplayName.Trim(),
                Email       = dto.Email.ToLower().Trim(),
                PasswordHash = dto.Password,
                Role        = "User",
            };

            var created = await _userService.Register(user);

            var profile = new UserProfileDto
            {
                UserId      = created.UserId,
                UserName    = created.UserName,
                DisplayName = created.DisplayName,
                Email       = created.Email,
                AvatarUrl   = created.AvatarUrl,
                IsOnline    = false,
                CreatedAt   = created.CreatedAt,
                IsActive    = true
            };

            return StatusCode(201,
                ApiResponseDto<UserProfileDto>.Ok(profile, "Account created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    // ── Login ───────────────────────────────────────────────────

    /// <summary>Login with email and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed"));

        try
        {
            var result = await _userService.Login(dto.Email, dto.Password);

            if (result is null)
                return Unauthorized(ApiResponseDto<object>.Fail("Invalid email or password."));

            return Ok(ApiResponseDto<AuthResponseDto>.Ok(result, "Login successful."));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == "ACCOUNT_DEACTIVATED")
        {
            return StatusCode(403, ApiResponseDto<object>.Fail(
                "Account deactivated. Please contact admin for reactivation."));
        }
    }

    // ── Admin: Get Users By Role ─────────────────────────────────

    [HttpGet("by-role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsersByRole([FromQuery] string role)
    {
        try
        {
            var users = await _userService.GetUsersByRole(role);
            return Ok(ApiResponseDto<object>.Ok(
                users.Select(u => new
                {
                    u.UserId, u.UserName, u.DisplayName,
                    u.Email, u.Role, u.IsActive, u.IsOnline, u.CreatedAt
                }).ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    // ── Admin: Get User By Email ─────────────────────────────────

    [HttpGet("by-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponseDto<object>.Fail("Email is required."));

        var user = await _userService.GetUserByEmail(email);
        if (user == null)
            return NotFound(ApiResponseDto<object>.Fail("User not found."));

        return Ok(ApiResponseDto<object>.Ok(new
        {
            user.UserId, user.UserName, user.DisplayName,
            user.Email, user.Role, user.IsActive, user.IsOnline, user.CreatedAt
        }));
    }

    // ── Admin: Change Role ───────────────────────────────────────

    [HttpPut("{id:int}/change-role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeRoleRequestDto dto)
    {
        try
        {
            if (GetCurrentUserId() == id)
                return StatusCode(403, ApiResponseDto<object>.Fail("Admin cannot change their own role."));

            var success = await _userService.ChangeUserRole(id, dto.Role);
            if (!success)
                return NotFound(ApiResponseDto<object>.Fail("User not found."));

            return Ok(ApiResponseDto<object>.Ok(null, $"User role changed to {dto.Role} successfully."));
        }
        catch (KeyNotFoundException ex)   { return NotFound(ApiResponseDto<object>.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponseDto<object>.Fail(ex.Message)); }
    }

    // ── Logout ──────────────────────────────────────────────────

    /// <summary>Logout and revoke tokens.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var user   = await _userService.GetUserById(userId);

        if (user == null)         return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!user.IsActive)       return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));

        var rawToken = Request.Headers["Authorization"]
            .FirstOrDefault()
            ?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
            ?.Trim();

        await _userService.Logout(dto.RefreshToken, userId, rawToken);
        return Ok(ApiResponseDto<object>.Ok(null!, "Logged out successfully."));
    }

    // ── Refresh Token ────────────────────────────────────────────

    /// <summary>Refresh JWT using refresh token.</summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var user   = await _userService.GetUserById(userId);

        if (user == null)   return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!user.IsActive) return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));

        var result = await _userService.RefreshToken(dto.RefreshToken, userId);
        if (result is null)
            return Unauthorized(ApiResponseDto<object>.Fail("Invalid or expired refresh token."));

        return Ok(ApiResponseDto<AuthResponseDto>.Ok(result, "Token refreshed successfully."));
    }

    // ── Validate Token ───────────────────────────────────────────

    /// <summary>Validate JWT token.</summary>
    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> ValidateToken([FromBody] ValidateTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponseDto<object>.Fail("Token is required"));

        var isValid = await _userService.ValidateToken(request.Token);
        if (!isValid)
            return Unauthorized(ApiResponseDto<object>.Fail("Token is invalid or has been invalidated"));

        return Ok(ApiResponseDto<object>.Ok(null, "Token is valid"));
    }

    // ── Get Profile ──────────────────────────────────────────────

    /// <summary>Get user profile by ID.</summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetProfile(int id)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin       = User.IsInRole("Admin");

        var currentUser = await _userService.GetUserById(currentUserId);
        if (currentUser == null)    return NotFound(ApiResponseDto<object>.Fail("User not found."));
        if (!currentUser.IsActive)  return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));

        var target = await _userService.GetUserById(id);
        if (target == null)                          return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!isAdmin && target.Role == "Admin")      return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!target.IsActive && !isAdmin)            return NotFound(ApiResponseDto<object>.Fail("User not found"));

        // Own profile or Admin → show email
        if (currentUserId == id || isAdmin)
        {
            return Ok(ApiResponseDto<object>.Ok(new
            {
                target.UserId, target.UserName, target.DisplayName,
                target.Email, target.AvatarUrl, target.Bio,
                target.IsOnline, target.LastSeen
            }));
        }

        return Ok(ApiResponseDto<object>.Ok(new
        {
            target.UserId, target.UserName, target.DisplayName,
            target.AvatarUrl, target.Bio, target.IsOnline, target.LastSeen
        }));
    }

    // ── Update Profile ────────────────────────────────────────────

    /// <summary>Update own profile.</summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfileRequestDto dto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentUser   = await _userService.GetUserById(currentUserId);

            if (currentUser == null)    return NotFound(ApiResponseDto<object>.Fail("User not found"));
            if (!currentUser.IsActive)  return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));
            if (currentUserId != id)    return StatusCode(403, ApiResponseDto<object>.Fail("You can only update your own profile"));

            var updatedUser = new User
            {
                DisplayName = dto.DisplayName ?? string.Empty,
                Bio         = dto.Bio,
                AvatarUrl   = dto.AvatarUrl
            };

            var result = await _userService.UpdateProfile(id, updatedUser);
            return Ok(ApiResponseDto<UserProfileDto>.Ok(MapToDto(result), "Profile updated successfully."));
        }
        catch (Exception ex) { return BadRequest(ApiResponseDto<object>.Fail(ex.Message)); }
    }

    // ── Change Password ───────────────────────────────────────────

    /// <summary>Change own password.</summary>
    [HttpPost("{id:int}/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequestDto dto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentUser   = await _userService.GetUserById(currentUserId);

            if (currentUser == null)    return NotFound(ApiResponseDto<object>.Fail("User not found"));
            if (!currentUser.IsActive)  return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));
            if (currentUserId != id)    return StatusCode(403, ApiResponseDto<object>.Fail("You can only change your own password"));

            var rawToken = Request.Headers["Authorization"]
                .FirstOrDefault()
                ?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                ?.Trim();

            await _userService.ChangePassword(id, dto.CurrentPassword, dto.NewPassword, rawToken);
            return Ok(ApiResponseDto<object>.Ok(null!, "Password changed successfully. Please login again."));
        }
        catch (UnauthorizedAccessException ex) { return BadRequest(ApiResponseDto<object>.Fail(ex.Message)); }
    }

    // ── Search Users ──────────────────────────────────────────────

    /// <summary>Search users by username or display name.</summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(ApiResponseDto<object>.Fail("Search query must be at least 2 characters."));

        var currentUser = await _userService.GetUserById(GetCurrentUserId());
        if (currentUser == null)    return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!currentUser.IsActive)  return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));

        var users   = await _userService.SearchUsers(q);
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
            users = users.Where(u => u.IsActive).ToList();

        if (!users.Any())
            return Ok(ApiResponseDto<object>.Ok(new List<object>(), "No users found"));

        return Ok(ApiResponseDto<object>.Ok(
            users.Select(u => new
            {
                u.UserId, u.UserName, u.DisplayName, u.AvatarUrl, u.Bio, u.IsOnline
            }).ToList()));
    }

    // ── Get All Active Users ──────────────────────────────────────

    /// <summary>Get all active users.</summary>
    [HttpGet("active")]
    [Authorize]
    public async Task<IActionResult> GetActiveUsers()
    {
        var currentUser = await _userService.GetUserById(GetCurrentUserId());
        if (currentUser == null)    return NotFound(ApiResponseDto<object>.Fail("User not found"));
        if (!currentUser.IsActive)  return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));

        var users = await _userService.GetAllActiveUsers();
        return Ok(ApiResponseDto<IList<UserProfileDto>>.Ok(users.Select(MapToDto).ToList()));
    }

    // ── Deactivate Account ────────────────────────────────────────

    [HttpDelete("{id:int}/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateAccount(int id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentUser   = await _userService.GetUserById(currentUserId);
            var isAdmin       = User.IsInRole("Admin");

            if (currentUser == null)                return NotFound(ApiResponseDto<object>.Fail("User not found"));
            if (!currentUser.IsActive)              return StatusCode(403, ApiResponseDto<object>.Fail("Account deactivated"));
            if (isAdmin && currentUserId == id)     return StatusCode(403, ApiResponseDto<object>.Fail("Admin cannot deactivate their own account"));
            if (!isAdmin && currentUserId != id)    return StatusCode(403, ApiResponseDto<object>.Fail("You can only deactivate your own account"));

            string? rawToken = null;
            if (currentUserId == id)
            {
                rawToken = Request.Headers["Authorization"]
                    .FirstOrDefault()
                    ?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                    ?.Trim();
            }

            await _userService.DeactivateAccount(id, rawToken);
            return Ok(ApiResponseDto<object>.Ok(null!, "Account deactivated successfully"));
        }
        catch (Exception ex) { return BadRequest(ApiResponseDto<object>.Fail(ex.Message)); }
    }

    // ── Reactivate Account ────────────────────────────────────────

    [HttpPut("{id:int}/reactivate")]
    [Authorize]
    public async Task<IActionResult> ReactivateUser(int id)
    {
        if (!User.IsInRole("Admin"))
            return StatusCode(403, ApiResponseDto<object>.Fail("Only admin can reactivate accounts"));

        try
        {
            var success = await _userService.ReactivateUser(id);
            if (!success) return NotFound(ApiResponseDto<object>.Fail("User not found"));
            return Ok(ApiResponseDto<object>.Ok(null, "User reactivated successfully"));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponseDto<object>.Fail(ex.Message)); }
    }

    // ── OAuth2: Google ────────────────────────────────────────────

    /// <summary>
    /// Step 1 — Initiate Google OAuth flow.
    /// Browser mein directly open karo: http://localhost:5000/api/users/oauth2/google
    /// </summary>
    [HttpGet("oauth2/google")]
    [AllowAnonymous]
    public IActionResult GoogleLogin()
    {
        // ✅ Callback route alag hai — /signin-google se conflict nahi hoga
        var redirectUrl = Url.Action(nameof(GoogleCallback), "User",
            values: null, protocol: Request.Scheme);

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, "Google");
    }

    /// <summary>
    /// Step 2 — Google is route pe redirect karta hai OAuth ke baad.
    /// Route: GET /api/users/oauth2/google/callback
    /// ✅ /signin-google se ALAG hai — Google middleware wahan correlation handle karta hai,
    ///    phir RedirectUri (yeh action) pe bhejta hai.
    /// </summary>
    [HttpGet("oauth2/google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback()
    {
        // Google middleware ne /signin-google pe correlation validate ki,
        // cookie sign-in kiya, aur ab yahan redirect kiya.
        // Ab sirf cookie se principal padhna hai.
        var result = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal == null)
        {
            _logger.LogError("Google OAuth callback failed: {Error}", result.Failure?.Message);
            return BadRequest(ApiResponseDto<object>.Fail("OAuth authentication failed."));
        }

        var claims = result.Principal.Claims;

        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                 ?? claims.FirstOrDefault(c => c.Type == "email")?.Value;

        if (string.IsNullOrEmpty(email))
            return BadRequest(ApiResponseDto<object>.Fail("Google did not return an email."));

        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                ?? claims.FirstOrDefault(c => c.Type == "name")?.Value
                ?? "User";

        // User exist karta hai ya nahi
        var user = await _userService.GetByEmail(email.ToLower().Trim());

        if (user is null)
        {
            var newUser = new User
            {
                Email        = email.ToLower().Trim(),
                UserName     = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N")[..6],
                DisplayName  = name,
                PasswordHash = string.Empty,
                Role         = "User",
            };
            user = await _userService.Register(newUser);
        }

        // JWT generate karo
        var loginResult = await _userService.GenerateTokensForUser(user);

        // Cookie clean karo — OAuth ke baad cookie ki zaroorat nahi
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Frontend pe redirect karo token ke saath
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        return Redirect($"{frontendUrl}/oauth-callback?token={loginResult.AccessToken}&refresh={loginResult.RefreshToken}");
    }

    // ── OAuth2: GitHub ────────────────────────────────────────────

    /// <summary>Initiate GitHub OAuth2 login flow.</summary>
    [HttpGet("oauth2/github")]
    [AllowAnonymous]
    public IActionResult GitHubLogin()
    {
        var redirectUrl = Url.Action(nameof(GitHubCallback), "User",
            values: null, protocol: Request.Scheme);

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, "GitHub");
    }

    [HttpGet("oauth2/github/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GitHubCallback()
    {
        var result = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal == null)
            return BadRequest(ApiResponseDto<object>.Fail("GitHub OAuth authentication failed."));

        var claims = result.Principal.Claims;
        var email  = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(email))
            return BadRequest(ApiResponseDto<object>.Fail("GitHub did not return an email."));

        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "User";
        var user = await _userService.GetByEmail(email.ToLower().Trim());

        if (user is null)
        {
            var newUser = new User
            {
                Email        = email.ToLower().Trim(),
                UserName     = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N")[..6],
                DisplayName  = name,
                PasswordHash = string.Empty,
                Role         = "User",
            };
            user = await _userService.Register(newUser);
        }

        var loginResult = await _userService.GenerateTokensForUser(user);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        return Redirect($"{frontendUrl}/oauth-callback?token={loginResult.AccessToken}&refresh={loginResult.RefreshToken}");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim == null) throw new UnauthorizedAccessException("Invalid token");
        return int.Parse(claim);
    }

    private static UserProfileDto MapToDto(User user) => new()
    {
        UserId      = user.UserId,
        UserName    = user.UserName,
        DisplayName = user.DisplayName,
        Email       = user.Email,
        AvatarUrl   = user.AvatarUrl,
        Bio         = user.Bio,
        IsOnline    = user.IsOnline,
        LastSeen    = user.LastSeen,
        CreatedAt   = user.CreatedAt,
        IsActive    = user.IsActive
    };
}