using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Presence.Models.Entities;

/// <summary>
/// UserPresence — EF Core entity for persisting presence state to DB.
/// One row per user. Updated async after ConcurrentDictionary is updated.
/// Used for LastSeen queries after service restart (in-memory is lost on restart).
/// </summary>
[Table("UserPresences")]
public class UserPresence
{
    [Key]
    public int UserId { get; set; }

    public bool IsOnline { get; set; } = false;

    public DateTime? LastSeen { get; set; }

    public DateTime? LastActiveAt { get; set; }

    public int ActiveConnectionCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
