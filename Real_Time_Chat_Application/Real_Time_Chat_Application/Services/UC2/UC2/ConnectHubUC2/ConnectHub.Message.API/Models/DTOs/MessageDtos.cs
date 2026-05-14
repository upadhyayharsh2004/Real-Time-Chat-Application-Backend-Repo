using System.ComponentModel.DataAnnotations;

namespace ConnectHub.Message.Models.DTOs;

// ─── Request DTOs ────────────────────────────────────────────────

/// <summary>
/// Send a direct message (ChatHub.SendDirectMessage)
/// </summary>
public class SendDirectMessageRequestDto
{
    [Required(ErrorMessage = "ReceiverId is required")]
    public int ReceiverId { get; set; }

    [Required(ErrorMessage = "Content is required")]
    [MaxLength(4000, ErrorMessage = "Message content cannot exceed 4000 characters")]
    [MinLength(1, ErrorMessage = "Message cannot be empty")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// TEXT | IMAGE | FILE | AUDIO — defaults to TEXT
    /// </summary>
    [MaxLength(20)]
    public string MessageType { get; set; } = "TEXT";

    /// <summary>
    /// Optional — Azure Blob URL for media messages
    /// </summary>
    [MaxLength(1000)]
    public string? MediaUrl { get; set; }

    /// <summary>
    /// Optional — reply threading
    /// </summary>
    public int? ReplyToMessageId { get; set; }
}

/// <summary>
/// Send a room/group message (ChatHub.SendRoomMessage)
/// </summary>
public class SendRoomMessageRequestDto
{
    [Required(ErrorMessage = "RoomId is required")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Content is required")]
    [MaxLength(4000)]
    [MinLength(1)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(20)]
    public string MessageType { get; set; } = "TEXT";

    [MaxLength(1000)]
    public string? MediaUrl { get; set; }

    public int? ReplyToMessageId { get; set; }
}

/// <summary>
/// Edit message — PUT /api/messages/{id}
/// Updates Content, sets IsEdited = true, EditedAt = DateTime.UtcNow
/// </summary>
public class EditMessageRequestDto
{
    [Required(ErrorMessage = "Content is required")]
    [MaxLength(4000, ErrorMessage = "Message content cannot exceed 4000 characters")]
    [MinLength(1, ErrorMessage = "Message cannot be empty")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Mark messages read — ChatHub.MarkMessageRead
/// </summary>
public class MarkReadRequestDto
{
    [Required]
    public int MessageId { get; set; }
}

/// <summary>
/// Typing indicator — ChatHub.TypingIndicator
/// </summary>
public class TypingIndicatorRequestDto
{
    [Required]
    public int RecipientId { get; set; }

    public bool IsTyping { get; set; }
}

/// <summary>
/// Add reaction to a message
/// </summary>
public class AddReactionRequestDto
{
    [Required]
    [MaxLength(10)]
    public string Emoji { get; set; } = string.Empty;
}

/// <summary>
/// Pin a message in a conversation or room
/// </summary>
public class PinMessageRequestDto
{
    public int? ConversationWithUserId { get; set; }
    public int? RoomId { get; set; }
}

// ─── Response DTOs ───────────────────────────────────────────────

/// <summary>
/// Full message DTO returned by all endpoints
/// </summary>
public class MessageDto
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public int? ReceiverId { get; set; }
    public int? RoomId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "TEXT";
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsEdited { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? MediaUrl { get; set; }
    public int? ReplyToMessageId { get; set; }
    public MessageDto? ReplyToMessage { get; set; }
    public List<ReactionDto> Reactions { get; set; } = new();
    public bool IsPinned { get; set; }
}
public class MessageQueueDto
{
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
}
/// <summary>
/// Emoji reaction summary
/// </summary>
public class ReactionDto
{
    public int ReactionId { get; set; }
    public int UserId { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public DateTime ReactedAt { get; set; }
}

/// <summary>
/// Recent chat list item — one entry per conversation partner
/// Powers the sidebar chat list (GetRecentChats)
/// </summary>
public class RecentChatDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
}
public class UserSummaryDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsOnline { get; set; }
}


/// <summary>
/// Paginated list of messages
/// </summary>
public class PagedMessagesDto
{
    public List<MessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// Unread message count response
/// </summary>
public class UnreadCountDto
{
    public int UnreadCount { get; set; }
}

/// <summary>
/// Pinned message response
/// </summary>
public class PinnedMessageDto
{
    public int PinId { get; set; }
    public MessageDto Message { get; set; } = null!;
    public int PinnedByUserId { get; set; }
    public DateTime PinnedAt { get; set; }
}

// ─── Standard API Response ────────────────────────────────────────

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

/// <summary>
/// DTO for POST /api/messages/test-send — Swagger/demo only REST fallback
/// when SignalR frontend is not available. Mirrors ChatHub.SendDirectMessage
/// and ChatHub.SendRoomMessage parameters.
/// Set ReceiverId for a direct message, RoomId for a room message (not both).
/// </summary>
public class TestSendRequestDto
{
    /// <summary>Direct message target. Mutually exclusive with RoomId.</summary>
    public int? ReceiverId { get; set; }

    /// <summary>Room message target. Mutually exclusive with ReceiverId.</summary>
    public int? RoomId { get; set; }

    /// <summary>Message text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>TEXT | IMAGE | FILE | AUDIO | VIDEO. Defaults to TEXT.</summary>
    public string? MessageType { get; set; } = "TEXT";

    /// <summary>Optional media URL for non-text messages.</summary>
    public string? MediaUrl { get; set; }

    /// <summary>Optional ID of message being replied to.</summary>
    public int? ReplyToMessageId { get; set; }
}
