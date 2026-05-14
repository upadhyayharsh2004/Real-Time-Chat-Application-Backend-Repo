using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.ChatRoom.Models.Entities;

/// <summary>
/// ChatRoom EF Core entity — ConnectHub ChatRoom Entities
/// As specified in the case study: ChatRoom entity created by POST /api/rooms
/// Creator automatically added as RoomMember with Role = ADMIN
/// RoomType: PUBLIC (browsable) | PRIVATE (invite only)
/// </summary>
[Table("ChatRooms")]
public class ChatRoom
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoomId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>PUBLIC | PRIVATE</summary>
    [Required]
    [MaxLength(10)]
    public string RoomType { get; set; } = "PUBLIC";

    /// <summary>UserId of the creator — also stored in RoomMembers as ADMIN role</summary>
    [Required]
    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    /// <summary>Soft delete timestamp</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Last message content — updated by RoomMessageSent consumer from UC2</summary>
    [MaxLength(4000)]
    public string? LastMessageContent { get; set; }

    /// <summary>Last message timestamp — for sidebar sorting</summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>Optional: room avatar / cover image URL stored in Azure Blob</summary>
    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }

    // ── Navigation ─────────────────────────────────────────────────
    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<RoomInvite> Invites { get; set; } = new List<RoomInvite>();

    // ── Helper methods ─────────────────────────────────────────────
    public int GetRoomId() => RoomId;
    public string GetName() => Name;
    public void SetName(string name) => Name = name;
    public string GetRoomType() => RoomType;
    public bool IsPublic() => RoomType == "PUBLIC";
    public bool IsActive_() => IsActive;
}
