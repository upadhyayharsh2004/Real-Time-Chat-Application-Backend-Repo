using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Notification.Models.Entities;

/// <summary>
/// Notification EF Core entity — ConnectHub Notification Entities.
/// Fields exactly as per class diagram (Figure 6):
///   NotificationId, RecipientId, SenderId (int?), Type, Title, Message,
///   RelatedId (int?), RelatedType (string?), IsRead (bool), SentAt
///
/// Type values: MESSAGE | MENTION | ROOM_INVITE | ROLE_CHANGE | PLATFORM
/// </summary>
[Table("Notifications")]
public class Notification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int NotificationId { get; set; }

    [Required]
    public int RecipientId { get; set; }

    /// <summary>Nullable — PLATFORM notifications have no sender</summary>
    public int? SenderId { get; set; }

    /// <summary>MESSAGE | MENTION | ROOM_INVITE | ROLE_CHANGE | PLATFORM</summary>
    [Required]
    [MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>FK to related entity — MessageId, RoomId, etc.</summary>
    public int? RelatedId { get; set; }

    /// <summary>e.g. "Message", "Room", "User"</summary>
    [MaxLength(50)]
    public string? RelatedType { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // ── Class diagram helper methods ──────────────────────────────
    public int GetNotificationId() => NotificationId;
    public int GetRecipientId() => RecipientId;
    public int? GetSenderId() => SenderId;
    public string GetType() => Type;
    public string GetTitle() => Title;
    public string GetMessage() => Message;
    public bool IsRead_() => IsRead;
    public void SetIsRead(bool read) => IsRead = read;
    public DateTime GetSentAt() => SentAt;
    public override string ToString() =>
        $"Notification[{NotificationId}] Type={Type} RecipientId={RecipientId} IsRead={IsRead}";
}
