namespace ConnectHub.Auth.Models.Events;

// ─── Outbound Events — Auth-Service → RabbitMQ ───────────────────────────────
// All events are published AFTER the DB save so all IDs and state are valid.
// Consumers: UC2 Message-Service, UC3 ChatRoom-Service, future Notification-Service.
//
// Queue names (all durable):
//   connecthub.user.registered       → new user created
//   connecthub.user.deactivated      → account deactivated  (UC2 + UC3 already consume this)
//   connecthub.user.reactivated      → account re-enabled by admin
//   connecthub.user.profile.updated  → display name / avatar / bio changed
//   connecthub.user.password.changed → password was changed (security event)
//   connecthub.user.role.changed     → user/admin role flip
//   connecthub.user.online           → user came online  (login / token refresh)
//   connecthub.user.offline          → user went offline (logout / deactivation)

/// <summary>
/// Published when a new user account is created (Register endpoint).
/// Consumers can pre-populate caches or send welcome notifications.
/// Queue: connecthub.user.registered
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
/// <summary>
/// Published when a user account is deactivated.
/// UC2: logs the event / preserves message history.
/// UC3: removes the user from all active room memberships.
/// Queue: connecthub.user.deactivated
/// </summary>
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}

/// <summary>
/// Published when an admin re-enables a previously deactivated account.
/// UC3 can re-allow the user to join rooms; notification service can alert the user.
/// Queue: connecthub.user.reactivated
/// </summary>
public class UserReactivatedEvent
{
    public string EventType { get; set; } = "UserReactivated";
    public int UserId { get; set; }
    public DateTime ReactivatedAt { get; set; }
}

/// <summary>
/// Published when a user updates their display name, bio, or avatar.
/// UC2 and UC3 display the sender name / avatar from their own DB so this lets them
/// refresh any cached user info.
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
/// Published when a user changes their password.
/// Consumers (e.g. Notification-Service) can send a security-alert email.
/// Queue: connecthub.user.password.changed
/// </summary>
public class UserPasswordChangedEvent
{
    public string EventType { get; set; } = "UserPasswordChanged";
    public int UserId { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Published when an admin changes a user's role (User ↔ Admin).
/// Consumers may need to update role-based caches or permissions.
/// Queue: connecthub.user.role.changed
/// </summary>
public class UserRoleChangedEvent
{
    public string EventType { get; set; } = "UserRoleChanged";
    public int UserId { get; set; }
    public string NewRole { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Published when a user logs in or comes online (presence update).
/// UC2 and UC3 can update online-presence indicators in SignalR hubs.
/// Queue: connecthub.user.online
/// </summary>
public class UserOnlineEvent
{
    public string EventType { get; set; } = "UserOnline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Published when a user logs out or goes offline (presence update).
/// UC2 and UC3 can update online-presence indicators in SignalR hubs.
/// Queue: connecthub.user.offline
/// </summary>
public class UserOfflineEvent
{
    public string EventType { get; set; } = "UserOffline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}
