using System.ComponentModel.DataAnnotations;

namespace ConnectHub.Notification.Models.DTOs;

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int RecipientId { get; set; }
    public int? SenderId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RelatedId { get; set; }
    public string? RelatedType { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
}

public class UnreadCountDto
{
    public int RecipientId { get; set; }
    public int UnreadCount { get; set; }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

/// <summary>POST /api/notifications/send-bulk — Admin broadcasts platform notification</summary>
public class SendBulkRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional: target specific users. Empty = all active users (broadcast)</summary>
    public List<int> RecipientIds { get; set; } = new();
}

// ─── Standard API Response — same pattern as UC1/UC2/UC3/UC4 ─────────────────

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
