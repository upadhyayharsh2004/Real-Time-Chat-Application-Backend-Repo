// namespace ConnectHub.Message.Models.Events;

// // ─── Outbound Events — Message-Service → RabbitMQ ────────────────────────────
// // Published AFTER DB save so MessageId is always valid.
// // Consumers: Notification-Service, Presence-Service, ChatRoom-Service.

// public class MessageSentEvent
// {
//     public string EventType { get; set; } = "MessageSent";
//     public int MessageId { get; set; }
//     public int SenderId { get; set; }
//     public string SenderName { get; set; } = string.Empty;
//     public int ReceiverId { get; set; }
//     public string Content { get; set; } = string.Empty;
//     public string MessageType { get; set; } = "TEXT";
//     public string? MediaUrl { get; set; }
//     public DateTime SentAt { get; set; }
// }
// public class UserReactivatedEvent
// {
//     public string EventType { get; set; } = "UserReactivated";

//     public int UserId { get; set; }

//     public DateTime ReactivatedAt { get; set; }
// }

// public class RoomMessageSentEvent
// {
//     public string EventType { get; set; } = "RoomMessageSent";
//     public int MessageId { get; set; }
//     public int SenderId { get; set; }
//     public string SenderName { get; set; } = string.Empty;
//     public int RoomId { get; set; }
//     public string Content { get; set; } = string.Empty;
//     public string MessageType { get; set; } = "TEXT";
//     public string? MediaUrl { get; set; }
//     public DateTime SentAt { get; set; }
//     public List<string> MentionedUserNames { get; set; } = new();
// }

// public class MessageReadEvent
// {
//     public string EventType { get; set; } = "MessageRead";
//     public int MessageId { get; set; }
//     public int SenderId { get; set; }
//     public int ReadByUserId { get; set; }
//     public DateTime ReadAt { get; set; }
// }

// public class MessageDeletedEvent
// {
//     public string EventType { get; set; } = "MessageDeleted";
//     public int MessageId { get; set; }
//     public int DeletedByUserId { get; set; }
//     public bool IsAdminDelete { get; set; }
//     public DateTime DeletedAt { get; set; }
// }

// public class MessageEditedEvent
// {
//     public string EventType { get; set; } = "MessageEdited";
//     public int MessageId { get; set; }
//     public int EditedByUserId { get; set; }
//     public string NewContent { get; set; } = string.Empty;
//     public DateTime EditedAt { get; set; }
// }

// // ─── Inbound Events — other services → Message-Service ───────────────────────
// // Consumed by MessageConsumer BackgroundService.

// /// <summary>
// /// From Auth-Service when a user is deactivated.
// /// Queue: connecthub.user.deactivated
// /// </summary>
// public class UserDeactivatedEvent
// {
//     public string EventType { get; set; } = "UserDeactivated";
//     public int UserId { get; set; }
//     public DateTime DeactivatedAt { get; set; }
// }
// public class UserOnlineEvent
// {
//     public string EventType { get; set; } = "UserOnline";

//     public int UserId { get; set; }

//     public DateTime LastSeen { get; set; }
// }
// public class UserOfflineEvent
// {
//     public string EventType { get; set; } = "UserOffline";

//     public int UserId { get; set; }

//     public DateTime LastSeen { get; set; }
// }

// /// <summary>
// /// From ChatRoom-Service when a room is deleted.
// /// Action: soft-delete all messages in that room.
// /// Queue: connecthub.room.deleted
// /// </summary>
// public class RoomDeletedEvent
// {
//     public string EventType { get; set; } = "RoomDeleted";
//     public int RoomId { get; set; }
//     public DateTime DeletedAt { get; set; }
// }


namespace ConnectHub.Message.Models.Events;

// ─── Outbound Events — Message-Service → RabbitMQ ────────────────────────────
// Published AFTER DB save so MessageId is always valid.
// Consumers: Notification-Service, Presence-Service, ChatRoom-Service.

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
public class UserReactivatedEvent
{
    public string EventType { get; set; } = "UserReactivated";

    public int UserId { get; set; }

    public DateTime ReactivatedAt { get; set; }
}

public class RoomMessageSentEvent
{
    public string EventType { get; set; } = "RoomMessageSent";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "TEXT";
    public string? MediaUrl { get; set; }
    public DateTime SentAt { get; set; }
    public List<string> MentionedUserNames { get; set; } = new();
}

public class MessageReadEvent
{
    public string EventType { get; set; } = "MessageRead";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int ReadByUserId { get; set; }
    public DateTime ReadAt { get; set; }
}

public class MessageDeletedEvent
{
    public string EventType { get; set; } = "MessageDeleted";
    public int MessageId { get; set; }
    public int DeletedByUserId { get; set; }
    public bool IsAdminDelete { get; set; }
    public DateTime DeletedAt { get; set; }
}

public class MessageEditedEvent
{
    public string EventType { get; set; } = "MessageEdited";
    public int MessageId { get; set; }
    public int EditedByUserId { get; set; }
    public string NewContent { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; }
}

// ─── Inbound Events — other services → Message-Service ───────────────────────
// Consumed by MessageConsumer BackgroundService.

/// <summary>
/// From Auth-Service when a user is deactivated.
/// Queue: connecthub.user.deactivated
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
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

/// <summary>
/// From Auth-Service when a user updates their display name, bio, or avatar.
/// Action: broadcast UserProfileUpdated via SignalR so all connected clients
///         can refresh the avatar/name without a page reload.
/// Queue: connecthub.user.profile.updated
/// </summary>
public class UserProfileUpdatedEvent
{
    public string EventType { get; set; } = "UserProfileUpdated";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// From ChatRoom-Service when a room is deleted.
/// Action: soft-delete all messages in that room.
/// Queue: connecthub.room.deleted
/// </summary>
public class RoomDeletedEvent
{
    public string EventType { get; set; } = "RoomDeleted";
    public int RoomId { get; set; }
    public DateTime DeletedAt { get; set; }
}