using ConnectHub.Notification.Models.Entities;

namespace ConnectHub.Notification.Repositories.Interfaces;

/// <summary>
/// INotificationRepository — ConnectHub Notification Repositories
/// Methods exactly as per class diagram (Figure 6):
///   FindByRecipientId(), FindUnreadByRecipientId(), CountUnreadByRecipientId(),
///   FindByRelatedId(), MarkAllReadByRecipientId(), DeleteByNotificationId(),
///   FindByType() — all Task&lt;T&gt;
/// </summary>
public interface INotificationRepository
{
    Task<IList<Models.Entities.Notification>> FindByRecipientId(int recipientId, int page = 1, int pageSize = 20);
    Task<IList<Models.Entities.Notification>> FindUnreadByRecipientId(int recipientId);
    Task<int> CountUnreadByRecipientId(int recipientId);
    Task<IList<Models.Entities.Notification>> FindByRelatedId(int relatedId, string? relatedType = null);
    Task MarkAllReadByRecipientId(int recipientId);
    Task DeleteByNotificationId(int notificationId);
    Task<IList<Models.Entities.Notification>> FindByType(string type);

    // ── Extended ───────────────────────────────────────────────────
    Task<Models.Entities.Notification?> FindById(int notificationId);
    Task<Models.Entities.Notification> Add(Models.Entities.Notification notification);
    Task<IList<Models.Entities.Notification>> AddRange(IList<Models.Entities.Notification> notifications);
    Task MarkAsRead(int notificationId);
    Task DeleteByRelatedId(int relatedId, string relatedType);
    Task<IList<Models.Entities.Notification>> GetAll(int page = 1, int pageSize = 20);
    Task SaveChangesAsync();
}
