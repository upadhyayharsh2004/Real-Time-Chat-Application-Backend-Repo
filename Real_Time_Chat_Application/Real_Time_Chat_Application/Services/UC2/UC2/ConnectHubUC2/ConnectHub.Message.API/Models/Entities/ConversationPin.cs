using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Message.Models.Entities;

/// <summary>
/// ConversationPin — allows users/admins to pin important messages
/// Extra feature: pinned messages surfaced at top of conversation
/// </summary>
[Table("ConversationPins")]
public class ConversationPin
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PinId { get; set; }

    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// UserId who pinned the message
    /// </summary>
    [Required]
    public int PinnedByUserId { get; set; }

    /// <summary>
    /// For direct conversations: the other user's ID
    /// </summary>
    public int? ConversationWithUserId { get; set; }

    /// <summary>
    /// For room pins
    /// </summary>
    public int? RoomId { get; set; }

    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(MessageId))]
    public Message Message { get; set; } = null!;
}
