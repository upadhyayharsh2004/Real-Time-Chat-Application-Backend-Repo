namespace ConnectHub.Media.Models.DTOs;

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class MediaFileDto
{
    public string FileId { get; set; } = string.Empty;
    public int UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeKb { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int? MessageId { get; set; }
    public int? RoomId { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SasUrlDto
{
    public string FileId { get; set; } = string.Empty;
    public string SasUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class FileStatsDto
{
    public int TotalFiles { get; set; }
    public long TotalSizeKb { get; set; }
    public int ImageCount { get; set; }
    public int VideoCount { get; set; }
    public int AudioCount { get; set; }
    public int DocumentCount { get; set; }
}

// ─── Standard API Response — same pattern as UC1-UC5 ─────────────────────────

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
