using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Auth.Models.Entities;

/// <summary>
/// User EF Core entity — ConnectHub Auth entities
/// Fields exactly as specified in the case study document
/// </summary>
[Table("Users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    public DateTime? DeactivatedAt { get; set; }
    public bool WasReactivated { get; set; } = false;
    public DateTime? LoggedOutAt { get; set; } // ← ADD THIS

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;


    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(300)]
    public string? Bio { get; set; }

    public bool IsOnline { get; set; } = false;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation — refresh tokens (extra: secure logout support)
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // Helper methods as specified in the class diagram
    public int GetUserId() => UserId;
    public void SetUserId(int id) => UserId = id;
    public string GetUserName() => UserName;
    public void SetUserName(string name) => UserName = name;
    public string GetDisplayName() => DisplayName;
    public string GetEmail() => Email;
    public string? GetAvatarUrl() => AvatarUrl;
    public bool IsActive_() => IsActive;
    public void SetIsOnline(bool online) => IsOnline = online;
    public DateTime GetLastSeen() => LastSeen;
    public override string ToString() => $"{UserName} ({Email})";
    public override bool Equals(object? obj) => obj is User u && u.UserId == UserId;
    public string Role { get; set; } = "User";

}
