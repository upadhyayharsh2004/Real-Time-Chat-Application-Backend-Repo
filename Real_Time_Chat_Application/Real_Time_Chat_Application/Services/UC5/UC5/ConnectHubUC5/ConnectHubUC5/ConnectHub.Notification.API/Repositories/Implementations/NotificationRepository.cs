using ConnectHub.Notification.Data;
using ConnectHub.Notification.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Notification.Repositories.Implementations;

/// <summary>
/// NotificationRepository — EF Core implementation of INotificationRepository.
/// Uses composite indexes for performance: (RecipientId, IsRead) and (RecipientId, SentAt).
/// </summary>
public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<IList<Models.Entities.Notification>> FindByRecipientId(
        int recipientId, int page = 1, int pageSize = 20) =>
        await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<IList<Models.Entities.Notification>> FindUnreadByRecipientId(int recipientId) =>
        await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();

    public async Task<int> CountUnreadByRecipientId(int recipientId) =>
        await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientId == recipientId && !n.IsRead);

    public async Task<IList<Models.Entities.Notification>> FindByRelatedId(
        int relatedId, string? relatedType = null)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RelatedId == relatedId);

        if (!string.IsNullOrEmpty(relatedType))
            query = query.Where(n => n.RelatedType == relatedType);

        return await query.OrderByDescending(n => n.SentAt).ToListAsync();
    }

    public async Task MarkAllReadByRecipientId(int recipientId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications)
            n.IsRead = true;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteByNotificationId(int notificationId)
    {
        var n = await _context.Notifications.FindAsync(notificationId);
        if (n is null) return;
        _context.Notifications.Remove(n);
        await _context.SaveChangesAsync();
    }

    public async Task<IList<Models.Entities.Notification>> FindByType(string type) =>
        await _context.Notifications
            .AsNoTracking()
            .Where(n => n.Type == type)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();

    public async Task<Models.Entities.Notification?> FindById(int notificationId) =>
        await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId);

    public async Task<Models.Entities.Notification> Add(Models.Entities.Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<IList<Models.Entities.Notification>> AddRange(
        IList<Models.Entities.Notification> notifications)
    {
        await _context.Notifications.AddRangeAsync(notifications);
        await _context.SaveChangesAsync();
        return notifications;
    }

    public async Task MarkAsRead(int notificationId)
    {
        var n = await _context.Notifications.FindAsync(notificationId);
        if (n is null) return;
        n.IsRead = true;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByRelatedId(int relatedId, string relatedType)
    {
        var notifications = await _context.Notifications
            .Where(n => n.RelatedId == relatedId && n.RelatedType == relatedType)
            .ToListAsync();

        _context.Notifications.RemoveRange(notifications);
        await _context.SaveChangesAsync();
    }

    public async Task<IList<Models.Entities.Notification>> GetAll(int page = 1, int pageSize = 20) =>
        await _context.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
