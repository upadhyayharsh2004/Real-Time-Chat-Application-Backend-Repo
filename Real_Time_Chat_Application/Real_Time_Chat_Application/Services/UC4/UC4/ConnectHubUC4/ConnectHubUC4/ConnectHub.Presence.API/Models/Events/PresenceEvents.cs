namespace ConnectHub.Presence.Models.Events;

// ─── Outbound Events — Presence-Service → RabbitMQ ───────────────────────────
// Published AFTER in-memory state is updated.

/// <summary>
/// Published when a user comes online (first connection).
/// Queue: connecthub.presence.online
/// Consumers: Notification-Service
/// </summary>
public class UserPresenceOnlineEvent
{
    public string EventType { get; set; } = "PresenceUserOnline";
    public int UserId { get; set; }
    public DateTime ConnectedAt { get; set; }
}

/// <summary>
/// Published when a user goes offline (last connection closed).
/// Queue: connecthub.presence.offline
/// Consumers: Notification-Service
/// </summary>
public class UserPresenceOfflineEvent
{
    public string EventType { get; set; } = "PresenceUserOffline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}

// ─── Inbound Events — consumed by Presence-Service ───────────────────────────
// Queue names MUST match what UC1 publishes (connecthub.user.*)
// and what UC2 publishes (connecthub.message.sent)

/// <summary>
/// From UC1 Auth-Service — user logged in → mark online.
/// Queue: connecthub.user.online  (EXACT name from UC1 RabbitMqPublisher.QueueUserOnline)
/// </summary>
public class UserOnlineFromAuthEvent
{
    public string EventType { get; set; } = "UserOnline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// From UC1 Auth-Service — user logged out → mark offline.
/// Queue: connecthub.user.offline  (EXACT name from UC1 RabbitMqPublisher.QueueUserOffline)
/// </summary>
public class UserOfflineFromAuthEvent
{
    public string EventType { get; set; } = "UserOffline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// From UC1 Auth-Service — account deactivated → force offline + clear all connections.
/// Queue: connecthub.user.deactivated  (EXACT name from UC1 RabbitMqPublisher.QueueUserDeactivated)
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}

/// <summary>
/// From UC1 Auth-Service — account reactivated → allow reconnection.
/// Queue: connecthub.user.reactivated  (EXACT name from UC1 RabbitMqPublisher.QueueUserReactivated)
/// </summary>
public class UserReactivatedEvent
{
    public string EventType { get; set; } = "UserReactivated";
    public int UserId { get; set; }
    public DateTime ReactivatedAt { get; set; }
}

/// <summary>
/// From UC2 Message-Service — direct message sent → update LastActiveAt for both sender and receiver.
/// Queue: connecthub.message.sent  (EXACT name from UC2 RabbitMqPublisher.QueueMessageSent)
/// </summary>
public class MessageSentEvent
{
    public string EventType { get; set; } = "MessageSent";
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public DateTime SentAt { get; set; }
}

/// <summary>
/// From UC1 Auth-Service — user profile updated → can refresh any cached display info.
/// Queue: connecthub.user.profile.updated  (EXACT name from UC1)
/// </summary>
public class UserProfileUpdatedEvent
{
    public string EventType { get; set; } = "UserProfileUpdated";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}
