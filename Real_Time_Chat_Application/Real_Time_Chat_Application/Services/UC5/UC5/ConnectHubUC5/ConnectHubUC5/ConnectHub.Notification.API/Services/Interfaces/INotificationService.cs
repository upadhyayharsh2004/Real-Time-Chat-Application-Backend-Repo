using ConnectHub.Notification.Models.DTOs;

namespace ConnectHub.Notification.Services.Interfaces;

/// <summary>
/// INotificationService — ConnectHub Notification Services
/// Methods exactly as per class diagram (Figure 6):
///   Send(), SendBulk(), GetByRecipient(), GetUnread(), GetUnreadCount(),
///   MarkAsRead(), MarkAllRead(), DeleteNotification(), SendEmail(), GetAll()
///
/// After Send(), calls IHubContext&lt;ChatHub&gt;.Clients.User(recipientId)
/// .SendAsync('NotificationCount', unreadCount) for real-time badge update.
/// Email via MailKit for offline users.
/// </summary>
public interface INotificationService
{
    // ── Class diagram methods ──────────────────────────────────────

    /// <summary>
    /// Send — creates Notification entity, persists to DB, pushes SignalR badge update.
    /// If recipient is offline, sends email via MailKit.
    /// </summary>
    Task<NotificationDto> Send(
        int recipientId, int? senderId, string type,
        string title, string message, int? relatedId = null, string? relatedType = null);

    /// <summary>
    /// SendBulk — Admin broadcasts PLATFORM notification to all or specific users.
    /// POST /api/notifications/send-bulk
    /// </summary>
    Task SendBulk(IList<int> recipientIds, string title, string message);

    Task<IList<NotificationDto>> GetByRecipient(int recipientId, int page = 1, int pageSize = 20);
    Task<IList<NotificationDto>> GetUnread(int recipientId);
    Task<int> GetUnreadCount(int recipientId);
    Task MarkAsRead(int notificationId);
    Task MarkAllRead(int recipientId);
    Task DeleteNotification(int notificationId);

    /// <summary>
    /// SendEmail — sends email via MailKit/MimeKit for offline users.
    /// Called internally by Send() when recipient is offline.
    /// </summary>
    Task SendEmail(string toEmail, string subject, string body);

    Task<IList<NotificationDto>> GetAll(int page = 1, int pageSize = 20);
}
