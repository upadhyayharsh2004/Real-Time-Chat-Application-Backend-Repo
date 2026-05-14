namespace ConnectHub.Media.Models.Events;

// ─── Outbound Events — Media-Service → RabbitMQ ──────────────────────────────

/// <summary>
/// Published after file is uploaded to Azure Blob and MediaFile saved to DB.
/// Queue: connecthub.media.uploaded
/// Consumers: Notification-Service (UC5), Message-Service (UC2) for media message metadata
/// </summary>
public class MediaUploadedEvent
{
    public string EventType { get; set; } = "MediaUploaded";
    public string FileId { get; set; } = string.Empty;
    public int UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeKb { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public int? MessageId { get; set; }
    public int? RoomId { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Published when a file is deleted.
/// Queue: connecthub.media.deleted
/// Consumers: Notification-Service (UC5)
/// </summary>
public class MediaDeletedEvent
{
    public string EventType { get; set; } = "MediaDeleted";
    public string FileId { get; set; } = string.Empty;
    public int DeletedByUserId { get; set; }
    public DateTime DeletedAt { get; set; }
}

// ─── Inbound Events — consumed by Media-Service ──────────────────────────────

/// <summary>
/// From UC1 Auth-Service — account deactivated.
/// Queue: connecthub.user.deactivated
/// Action: soft-delete or flag all files uploaded by this user
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}

/// <summary>
/// From UC3 ChatRoom-Service — room deleted.
/// Queue: connecthub.room.deleted
/// Action: mark all files in that room as expired for cleanup
/// </summary>
public class RoomDeletedEvent
{
    public string EventType { get; set; } = "RoomDeleted";
    public int RoomId { get; set; }
    public DateTime DeletedAt { get; set; }
}
