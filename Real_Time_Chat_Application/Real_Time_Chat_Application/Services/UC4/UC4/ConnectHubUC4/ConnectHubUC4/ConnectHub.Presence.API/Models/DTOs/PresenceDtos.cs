namespace ConnectHub.Presence.Models.DTOs;

public class UserPresenceDto
{
    public int UserId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public int ActiveConnectionCount { get; set; }
}

public class BulkPresenceRequestDto
{
    public List<int> UserIds { get; set; } = new();
}

// ─── Standard API Response — same pattern as UC1/UC2/UC3 ─────────────────────

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
