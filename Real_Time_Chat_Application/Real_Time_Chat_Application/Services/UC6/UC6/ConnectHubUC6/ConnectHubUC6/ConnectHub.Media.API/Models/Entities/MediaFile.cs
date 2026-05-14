using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Media.Models.Entities;

/// <summary>
/// MediaFile EF Core entity — ConnectHub Media Entities.
/// Fields exactly as per class diagram (Figure 7):
///   FileId (Guid as string), UploadedBy (FK to User),
///   FileName, ContentType, FileSizeKb (long), BlobUrl,
///   ThumbnailUrl (string?), MessageId (int?), RoomId (int?),
///   UploadedAt, ExpiresAt (DateTime?)
///
/// FileId is a Guid stored as string — globally unique, safe for Azure Blob naming.
/// BlobUrl stores the Azure Blob Storage URL (not publicly accessible without SAS).
/// GenerateSasUrl() produces a time-limited SAS token (ExpiresOn = UtcNow.AddHours(1)).
/// ExpiresAt is set for temp files cleaned up by IHostedService daily.
/// </summary>
[Table("MediaFiles")]
public class MediaFile
{
    [Key]
    [MaxLength(36)]
    public string FileId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>FK to User (int) in Auth-Service DB — stored as int here</summary>
    [Required]
    public int UploadedBy { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeKb { get; set; }

    [Required]
    [MaxLength(2000)]
    public string BlobUrl { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>Nullable — populated when file is attached to a direct message</summary>
    public int? MessageId { get; set; }

    /// <summary>Nullable — populated when file is in a room/group chat</summary>
    public int? RoomId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nullable — set for temp files; IHostedService deletes expired files daily</summary>
    public DateTime? ExpiresAt { get; set; }

    // ── Class diagram helper methods ──────────────────────────────
    public string GetFileId() => FileId;
    public void SetFileId(string id) => FileId = id;
    public int GetUploadedBy() => UploadedBy;
    public string GetFileName() => FileName;
    public void SetFileName(string name) => FileName = name;
    public string GetContentType() => ContentType;
    public long GetFileSizeKb() => FileSizeKb;
    public string GetBlobUrl() => BlobUrl;
    public string? GetThumbnailUrl() => ThumbnailUrl;
    public int? GetMessageId() => MessageId;
    public int? GetRoomId() => RoomId;
    public DateTime GetUploadedAt() => UploadedAt;
    public DateTime? GetExpiresAt() => ExpiresAt;
    public override string ToString() =>
        $"MediaFile[{FileId}] FileName={FileName} ContentType={ContentType} SizeKb={FileSizeKb}";
}
