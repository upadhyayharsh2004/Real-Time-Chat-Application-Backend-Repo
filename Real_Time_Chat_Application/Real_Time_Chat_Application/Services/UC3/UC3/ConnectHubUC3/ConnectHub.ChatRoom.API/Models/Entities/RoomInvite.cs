using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.ChatRoom.Models.Entities;

/// <summary>
/// RoomInvite — invite a user to a PRIVATE room.
/// Extra feature beyond spec: supports private room membership via invite links.
/// Status: PENDING | ACCEPTED | DECLINED | EXPIRED
/// Invites expire after InviteExpiryHours (default 48h).
/// </summary>
[Table("RoomInvites")]
public class RoomInvite
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int InviteId { get; set; }

    [Required]
    public int RoomId { get; set; }

    /// <summary>UserId who sent the invite (must be ADMIN or MODERATOR)</summary>
    [Required]
    public int InvitedByUserId { get; set; }

    /// <summary>UserId being invited</summary>
    [Required]
    public int InvitedUserId { get; set; }

    /// <summary>PENDING | ACCEPTED | DECLINED | EXPIRED</summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(48);

    public DateTime? RespondedAt { get; set; }

    [ForeignKey(nameof(RoomId))]
    public ChatRoom Room { get; set; } = null!;
}
