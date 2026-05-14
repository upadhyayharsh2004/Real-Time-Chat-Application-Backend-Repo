using System.ComponentModel.DataAnnotations;

namespace ConnectHub.Auth.Models.DTOs;

// ─── Request DTOs ───────────────────────────────────────────────
public class ChangeRoleRequestDto
{
    [Required]
    public string Role { get; set; } = string.Empty;
}
public class ValidateTokenRequestDto
{
    public string Token { get; set; } = string.Empty;
}
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Username is required")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required")]
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    [MaxLength(100, ErrorMessage = "Password cannot exceed 100 characters")]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; }
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UpdateProfileRequestDto
{
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string? DisplayName { get; set; }

    [MaxLength(300, ErrorMessage = "Bio cannot exceed 300 characters")]
    public string? Bio { get; set; }

    [MaxLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters")]
    public string? AvatarUrl { get; set; }
}

public class ChangePasswordRequestDto
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters")]
    [MaxLength(100, ErrorMessage = "New password cannot exceed 100 characters")]
    public string NewPassword { get; set; } = string.Empty;
}

// ─── Response DTOs ──────────────────────────────────────────────

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

// ─── Standard API Response ──────────────────────────────────────

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();


    public static ApiResponseDto<T> Ok(T data, string message = "Success") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    public static ApiResponseDto<T> Fail(string message, List<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors ?? new()
        };
}