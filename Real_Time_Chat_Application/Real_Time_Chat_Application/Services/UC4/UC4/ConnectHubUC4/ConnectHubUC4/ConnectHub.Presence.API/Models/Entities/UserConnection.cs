using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectHub.Presence.Models.Entities;

/// <summary>
/// UserConnection — EF Core entity AND in-memory value type.
/// Fields exactly as per class diagram:
///   ConnectionId:string, UserId:int, ConnectedAt:DateTime, DeviceInfo:string?
/// Also used as the VALUE in ConcurrentDictionary&lt;string, UserConnection&gt;
/// (_userInfo) for in-memory connection metadata — no DB round trip on hot path.
/// </summary>
[Table("UserConnections")]
public class UserConnection
{
    [Key]
    [MaxLength(200)]
    public string ConnectionId { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }

    // ── Class diagram helper methods ──────────────────────────────
    public string GetConnectionId() => ConnectionId;
    public int GetUserId() => UserId;
    public DateTime GetConnectedAt() => ConnectedAt;
    public string? GetDeviceInfo() => DeviceInfo;
    public override string ToString() =>
        $"ConnectionId={ConnectionId}, UserId={UserId}, ConnectedAt={ConnectedAt}";
}
