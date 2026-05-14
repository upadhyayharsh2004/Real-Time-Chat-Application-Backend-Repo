using System.ComponentModel.DataAnnotations;

namespace ConnectHub.ChatRoom.Models.DTOs;

// ─── Request DTOs ─────────────────────────────────────────────────────────────

/// <summary>POST /api/rooms — creates ChatRoom entity; creator added as RoomMember with Role = ADMIN</summary>
public class CreateRoomRequestDto
{
    [Required(ErrorMessage = "Room name is required")]
    [MinLength(2, ErrorMessage = "Room name must be at least 2 characters")]
    [MaxLength(100, ErrorMessage = "Room name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>PUBLIC | PRIVATE — defaults to PUBLIC</summary>
    [MaxLength(10)]
    public string RoomType { get; set; } = "PUBLIC";

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }
}

/// <summary>PUT /api/rooms/{id} — room admin updates name, description, type</summary>
public class UpdateRoomRequestDto
{
    [MinLength(2)]
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? RoomType { get; set; }

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }
}

/// <summary>Invite a user to a PRIVATE room</summary>
public class InviteUserRequestDto
{
    [Required]
    public int InvitedUserId { get; set; }
}

/// <summary>Accept or decline an invite</summary>
public class RespondToInviteRequestDto
{
    [Required]
    public bool Accept { get; set; }
}

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class ChatRoomDto
{
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoomType { get; set; } = "PUBLIC";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int MemberCount { get; set; }
    public bool IsCurrentUserMember { get; set; }
    public string? CurrentUserRole { get; set; }
}

public class RoomMemberDto
{
    public int RoomMemberId { get; set; }
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "MEMBER";
    public DateTime JoinedAt { get; set; }
    public bool IsOnline { get; set; }
}

public class RoomInviteDto
{
    public int InviteId { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int InvitedByUserId { get; set; }
    public int InvitedUserId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class PagedRoomsDto
{
    public List<ChatRoomDto> Rooms { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

// ─── Standard API Response — same pattern as UC1 + UC2 ───────────────────────

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponseDto<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponseDto<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Data = default, Errors = errors ?? new() };
}
