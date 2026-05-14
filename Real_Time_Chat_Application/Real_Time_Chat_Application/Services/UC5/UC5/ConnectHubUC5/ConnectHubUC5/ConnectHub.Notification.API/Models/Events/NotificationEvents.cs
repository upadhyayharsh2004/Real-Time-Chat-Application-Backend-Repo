namespace ConnectHub.Notification.Models.Events;

// ─── Inbound Events consumed by Notification-Service ────────────────────────
// Queue names MUST match EXACTLY what UC1/UC2/UC3/UC4 publish.

// ── From UC1 Auth-Service ─────────────────────────────────────────────────────

/// <summary>
/// From UC1 — new user registered.
/// Queue: connecthub.user.registered
/// Action: (optional) send welcome email
/// </summary>
public class UserRegisteredEvent
{
    public string EventType { get; set; } = "UserRegistered";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime RegisteredAt { get; set; }
}

public class RoomInviteSentEvent
{
    public string EventType { get; set; } = "RoomInviteSent";
    public int InviteId { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int InvitedUserId { get; set; }
    public int InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
/// <summary>
/// From UC1 — account deactivated.
/// Queue: connecthub.user.deactivated
/// Action: clear all unread badge counts for this user
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}

/// <summary>
/// From UC1 — role changed.
/// Queue: connecthub.user.role.changed
/// Action: create ROLE_CHANGE notification for the affected user
/// </summary>
public class UserRoleChangedEvent
{
    public string EventType { get; set; } = "UserRoleChanged";
    public int UserId { get; set; }
    public string NewRole { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

// ── From UC2 Message-Service ──────────────────────────────────────────────────

/// <summary>
/// From UC2 — direct message sent.
/// Queue: connecthub.message.sent
/// Action: create MESSAGE notification; push badge update via SignalR;
///         send email if recipient is offline (via MailKit)
/// </summary>
public class MessageSentEvent
{
    public string EventType { get; set; } = "MessageSent";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public int ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "TEXT";
    public string? MediaUrl { get; set; }
    public DateTime SentAt { get; set; }
}

/// <summary>
/// From UC2 — room message sent (may contain @mentions).
/// Queue: connecthub.room.message.sent
/// Action: create MENTION notification for each @mentioned user;
///         push badge update via SignalR
/// </summary>
public class RoomMessageSentEvent
{
    public string EventType { get; set; } = "RoomMessageSent";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "TEXT";
    public DateTime SentAt { get; set; }
    public List<string> MentionedUserNames { get; set; } = new();
}

/// <summary>
/// From UC2 — message read (read receipt).
/// Queue: connecthub.message.read
/// Action: mark related MESSAGE notification as read
/// </summary>
public class MessageReadEvent
{
    public string EventType { get; set; } = "MessageRead";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int ReadByUserId { get; set; }
    public DateTime ReadAt { get; set; }
}

/// <summary>
/// From UC2 — message deleted.
/// Queue: connecthub.message.deleted
/// Action: delete related MESSAGE notification if still unread
/// </summary>
public class MessageDeletedEvent
{
    public string EventType { get; set; } = "MessageDeleted";
    public int MessageId { get; set; }
    public int DeletedByUserId { get; set; }
    public bool IsAdminDelete { get; set; }
    public DateTime DeletedAt { get; set; }
}

// ── From UC3 ChatRoom-Service ─────────────────────────────────────────────────

/// <summary>
/// From UC3 — user invited to a room.
/// Queue: connecthub.room.member.joined
/// Action: create ROOM_INVITE notification for the invited user
/// </summary>
public class RoomMemberJoinedEvent
{
    public string EventType { get; set; } = "RoomMemberJoined";
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// From UC3 — room created.
/// Queue: connecthub.room.created
/// Action: no notification needed (creator already knows)
/// </summary>
public class RoomCreatedEvent
{
    public string EventType { get; set; } = "RoomCreated";
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoomType { get; set; } = "PUBLIC";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── From UC4 Presence-Service ─────────────────────────────────────────────────

/// <summary>
/// From UC4 — user came online.
/// Queue: connecthub.presence.online
/// Action: push online status update to DM partners via SignalR
/// </summary>
public class UserPresenceOnlineEvent
{
    public string EventType { get; set; } = "PresenceUserOnline";
    public int UserId { get; set; }
    public DateTime ConnectedAt { get; set; }
}

/// <summary>
/// From UC4 — user went offline.
/// Queue: connecthub.presence.offline
/// Action: push offline status update; trigger queued email notifications if any
/// </summary>
public class UserPresenceOfflineEvent
{
    public string EventType { get; set; } = "PresenceUserOffline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}
