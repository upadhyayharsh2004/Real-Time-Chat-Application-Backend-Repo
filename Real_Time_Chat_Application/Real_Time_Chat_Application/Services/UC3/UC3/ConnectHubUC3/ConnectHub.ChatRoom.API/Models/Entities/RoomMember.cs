using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.ChatRoom.Models.Entities;

/// <summary>
/// RoomMember — join table between ChatRoom and User.
/// Role: ADMIN (can update/delete room) | MODERATOR | MEMBER
/// Creator of a room is automatically added as ADMIN.
/// GET /api/rooms/{roomId}/members returns IList&lt;RoomMember&gt; with roles and join dates.
/// </summary>
[Table("RoomMembers")]
public class RoomMember
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoomMemberId { get; set; }

    [Required]
    public int RoomId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>ADMIN | MODERATOR | MEMBER</summary>
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "MEMBER";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft-leave: set when user leaves room, cleared on rejoin</summary>
    public DateTime? LeftAt { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(RoomId))]
    public ChatRoom Room { get; set; } = null!;
}
