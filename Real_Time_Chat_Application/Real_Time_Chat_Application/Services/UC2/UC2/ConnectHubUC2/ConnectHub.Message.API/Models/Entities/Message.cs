using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Message.Models.Entities;

/// <summary>
/// Message EF Core entity — ConnectHub Message entities
/// Fields exactly as specified in the case study document (Message-Service Class Diagram)
/// SenderID + ReceiverID (nullable) for direct messages
/// SenderID + RoomId (nullable) for room/group messages
/// Exactly one of ReceiverId / RoomId is populated per message
/// </summary>
[Table("Messages")]
public class Message
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MessageId { get; set; }

    [Required]
    public int SenderId { get; set; }

    /// <summary>
    /// Nullable — populated only for direct messages; null for room messages
    /// </summary>
    public int? ReceiverId { get; set; }

    /// <summary>
    /// Nullable — populated only for room/group messages; null for direct messages
    /// </summary>
    public int? RoomId { get; set; }

    /// <summary>
    /// Message content — replaced with placeholder on soft delete
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// TEXT | IMAGE | FILE | AUDIO
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string MessageType { get; set; } = "TEXT";

    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Soft delete — IsDeleted = true; content replaced with "[Message deleted]"
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    public bool IsEdited { get; set; } = false;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// For IMAGE/FILE/AUDIO messages — Azure Blob URL
    /// </summary>
    [MaxLength(1000)]
    public string? MediaUrl { get; set; }

    /// <summary>
    /// Optional reply-to thread support
    /// </summary>
    public int? ReplyToMessageId { get; set; }

    // ── Navigation properties ─────────────────────────────────
    [ForeignKey(nameof(ReplyToMessageId))]
    public Message? ReplyToMessage { get; set; }

    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();

    // ── Helper methods as per class diagram ──────────────────
    public int GetMessageId() => MessageId;
    public void SetMessageId(int id) => MessageId = id;
    public int GetSenderId() => SenderId;
    public void SetSenderId(int id) => SenderId = id;
    public int? GetReceiverId() => ReceiverId;
    public int? GetRoomId() => RoomId;
    public string GetContent() => Content;
    public void SetContent(string content) => Content = content;
    public string GetMessageType() => MessageType;
    public void SetMessageType(string type) => MessageType = type;
    public bool IsRead_() => IsRead;
    public void SetIsRead(bool read) => IsRead = read;
    public bool IsDeleted_() => IsDeleted;
    public void SetIsDeleted(bool deleted) => IsDeleted = deleted;
    public bool IsEdited_() => IsEdited;
    public DateTime GetSentAt() => SentAt;
    public string? GetMediaUrl() => MediaUrl;
}
