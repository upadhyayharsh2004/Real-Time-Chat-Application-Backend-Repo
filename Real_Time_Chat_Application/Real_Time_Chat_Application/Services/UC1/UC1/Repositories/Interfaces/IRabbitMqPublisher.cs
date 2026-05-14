using ConnectHub.Auth.Models.Events;

namespace ConnectHub.Auth.Repositories.Interfaces;

/// <summary>
/// IRabbitMqPublisher — typed event publishing contract for Auth-Service.
///
/// One method per domain event. Publishing always happens AFTER the DB save so
/// all IDs and state values are guaranteed valid.
///
/// Queues published (all durable):
///   connecthub.user.registered       → new account created
///   connecthub.user.deactivated      → account deactivated   (consumed by UC2 + UC3)
///   connecthub.user.reactivated      → account re-enabled by admin
///   connecthub.user.profile.updated  → display name / bio / avatar changed
///   connecthub.user.password.changed → password changed (security event)
///   connecthub.user.role.changed     → role flipped (User ↔ Admin)
///   connecthub.user.online           → user logged in / came online
///   connecthub.user.offline          → user logged out / went offline
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishUserRegisteredAsync(UserRegisteredEvent @event);
    Task PublishUserDeactivatedAsync(UserDeactivatedEvent @event);
    Task PublishUserReactivatedAsync(UserReactivatedEvent @event);
    Task PublishUserProfileUpdatedAsync(UserProfileUpdatedEvent @event);
    Task PublishUserPasswordChangedAsync(UserPasswordChangedEvent @event);
    Task PublishUserRoleChangedAsync(UserRoleChangedEvent @event);
    Task PublishUserOnlineAsync(UserOnlineEvent @event);
    Task PublishUserOfflineAsync(UserOfflineEvent @event);
}
