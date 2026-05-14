namespace ConnectHub.ChatRoom.Models.Events;

// ─── Outbound Events — ChatRoom-Service → RabbitMQ ───────────────────────────
// Published AFTER DB save. Consumed by Message-Service (UC2), Notification-Service.

/// <summary>
/// Published when a new room is created.
/// Queue: connecthub.room.created
/// Consumers: Notification-Service (notify members)
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
public class UserOnlineEvent
{
    public string EventType { get; set; } = "UserOnline";

    public int UserId { get; set; }

    public DateTime LastSeen { get; set; }
}
public class UserOfflineEvent
{
    public string EventType { get; set; } = "UserOffline";

    public int UserId { get; set; }

    public DateTime LastSeen { get; set; }
}
public class UserReactivatedEvent
{
    public string EventType { get; set; } = "UserReactivated";

    public int UserId { get; set; }

    public DateTime ReactivatedAt { get; set; }
}

/// <summary>
/// Published when a room is deleted (soft delete).
/// Queue: connecthub.room.deleted
/// Consumers: Message-Service (UC2) → soft-deletes all messages in room
/// </summary>
public class RoomDeletedEvent
{
    public string EventType { get; set; } = "RoomDeleted";
    public int RoomId { get; set; }
    public int DeletedByUserId { get; set; }
    public DateTime DeletedAt { get; set; }
}

/// <summary>
/// Published when a user joins a room.
/// Queue: connecthub.room.member.joined
/// Consumers: Notification-Service (notify room members)
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
/// Published when a user leaves a room.
/// Queue: connecthub.room.member.left
/// Consumers: Notification-Service
/// </summary>
public class RoomMemberLeftEvent
{
    public string EventType { get; set; } = "RoomMemberLeft";
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public DateTime LeftAt { get; set; }
}

/// <summary>
/// Published when room details are updated.
/// Queue: connecthub.room.updated
/// </summary>
public class RoomUpdatedEvent
{
    public string EventType { get; set; } = "RoomUpdated";
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ─── Inbound Events — consumed by ChatRoom-Service from RabbitMQ ──────────────

/// <summary>
/// Consumed from UC2 Message-Service when a room message is sent.
/// Used to update LastMessageContent and LastMessageAt on ChatRoom entity
/// for sidebar display without querying the Message-Service.
/// Queue: connecthub.room.message.sent
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
}

/// <summary>
/// Consumed from Auth-Service (UC1) when a user account is deactivated.
/// ChatRoom-Service removes the user from all active rooms.
/// Queue: connecthub.user.deactivated
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}
