using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Message.Models.Entities;

/// <summary>
/// MessageReaction — allows users to react to messages with emoji
/// Extra feature beyond spec: enhances message engagement
/// </summary>
[Table("MessageReactions")]
public class MessageReaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ReactionId { get; set; }

    [Required]
    public int MessageId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Emoji character or code, e.g. "👍", "❤️", "😂"
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Emoji { get; set; } = string.Empty;

    public DateTime ReactedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(MessageId))]
    public Message Message { get; set; } = null!;
}
