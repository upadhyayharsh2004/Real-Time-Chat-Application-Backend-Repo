using ConnectHub.Notification.Hubs;
using ConnectHub.Notification.Models.DTOs;
using ConnectHub.Notification.Repositories.Interfaces;
using ConnectHub.Notification.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.SignalR;
using MimeKit;

namespace ConnectHub.Notification.Services.Implementations;

/// <summary>
/// NotificationService — implements INotificationService.
///
/// After Send():
///   1. Persists Notification entity to SQL Server via EF Core
///   2. Calls IHubContext&lt;NotificationHub&gt;.Clients.User(recipientId)
///      .SendAsync("NotificationCount", unreadCount)
///      for real-time badge count update to recipient's active connections
///   3. If recipient is offline → sends email via MailKit/MimeKit
///
/// SendBulk() — Admin broadcasts PLATFORM notification.
///   Used by Admin for platform-wide notifications.
///   NotificationService.SendBulk() called from NotificationController.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repo,
        IHubContext<NotificationHub> hubContext,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _repo = repo;
        _hubContext = hubContext;
        _config = config;
        _logger = logger;
    }

    public async Task<NotificationDto> Send(
        int recipientId, int? senderId, string type,
        string title, string message, int? relatedId = null, string? relatedType = null)
    {
        // ✅ STEP 1: Persist to DB
        var notification = new Models.Entities.Notification
        {
            RecipientId = recipientId,
            SenderId = senderId,
            Type = type,
            Title = title,
            Message = message,
            RelatedId = relatedId,
            RelatedType = relatedType,
            IsRead = false,
            SentAt = DateTime.UtcNow
        };

        var saved = await _repo.Add(notification);
        _logger.LogInformation(
            "Notification {Id} created for RecipientId={RecipientId} Type={Type}",
            saved.NotificationId, recipientId, type);

        // ✅ STEP 2: Push badge count via SignalR IHubContext
        // IHubContext<NotificationHub> does not require an active hub connection —
        // it pushes directly to all active connections for this user
        var unreadCount = await _repo.CountUnreadByRecipientId(recipientId);
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("NotificationCount", unreadCount);

        _logger.LogInformation(
            "[SignalR] Pushed NotificationCount={Count} to RecipientId={RecipientId}",
            unreadCount, recipientId);

        // ✅ STEP 3: Send email if recipient email is configured and they may be offline
        // In production: check PresenceService.IsUserOnline(recipientId)
        // For now: always attempt email (non-blocking, fire-and-forget)
        var emailConfig = _config.GetSection("Email");
        if (emailConfig.GetValue<bool>("Enabled"))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // In production: resolve email from Auth-Service user store
                    // For now: email is passed via config or resolved externally
                    var recipientEmail = emailConfig["DefaultRecipient"];
                    if (!string.IsNullOrEmpty(recipientEmail))
                        await SendEmail(recipientEmail, title, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Email send failed for RecipientId={RecipientId}", recipientId);
                }
            });
        }

        return MapToDto(saved);
    }

    public async Task SendBulk(IList<int> recipientIds, string title, string message)
    {
        if (!recipientIds.Any())
        {
            _logger.LogWarning("SendBulk called with empty recipientIds list.");
            return;
        }

        // Create one PLATFORM notification per recipient
        var notifications = recipientIds.Select(id => new Models.Entities.Notification
        {
            RecipientId = id,
            SenderId = null,
            Type = "PLATFORM",
            Title = title,
            Message = message,
            IsRead = false,
            SentAt = DateTime.UtcNow
        }).ToList();

        await _repo.AddRange(notifications);
        _logger.LogInformation(
            "SendBulk: Created {Count} PLATFORM notifications. Title={Title}",
            notifications.Count, title);

        // Push badge count update to ALL recipients via SignalR
        foreach (var notification in notifications)
        {
            var unreadCount = await _repo.CountUnreadByRecipientId(notification.RecipientId);
            await _hubContext.Clients.User(notification.RecipientId.ToString())
                .SendAsync("NotificationCount", unreadCount);
        }
    }

    public async Task<IList<NotificationDto>> GetByRecipient(
        int recipientId, int page = 1, int pageSize = 20)
    {
        var notifications = await _repo.FindByRecipientId(recipientId, page, pageSize);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<IList<NotificationDto>> GetUnread(int recipientId)
    {
        var notifications = await _repo.FindUnreadByRecipientId(recipientId);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCount(int recipientId) =>
        await _repo.CountUnreadByRecipientId(recipientId);

    public async Task MarkAsRead(int notificationId)
    {
        await _repo.MarkAsRead(notificationId);
        _logger.LogInformation("Notification {Id} marked as read.", notificationId);

        // Update badge count for recipient
        var notification = await _repo.FindById(notificationId);
        if (notification is not null)
        {
            var unreadCount = await _repo.CountUnreadByRecipientId(notification.RecipientId);
            await _hubContext.Clients.User(notification.RecipientId.ToString())
                .SendAsync("NotificationCount", unreadCount);
        }
    }

    public async Task MarkAllRead(int recipientId)
    {
        await _repo.MarkAllReadByRecipientId(recipientId);
        _logger.LogInformation("All notifications marked as read for RecipientId={RecipientId}", recipientId);

        // Push updated badge count (0) via SignalR
        await _hubContext.Clients.User(recipientId.ToString())
            .SendAsync("NotificationCount", 0);
    }

    public async Task DeleteNotification(int notificationId)
    {
        var notification = await _repo.FindById(notificationId);
        await _repo.DeleteByNotificationId(notificationId);
        _logger.LogInformation("Notification {Id} deleted.", notificationId);

        // Update badge count
        if (notification is not null)
        {
            var unreadCount = await _repo.CountUnreadByRecipientId(notification.RecipientId);
            await _hubContext.Clients.User(notification.RecipientId.ToString())
                .SendAsync("NotificationCount", unreadCount);
        }
    }

    /// <summary>
    /// SendEmail — MailKit/MimeKit implementation.
    /// Configured via Email section in appsettings.json.
    /// Called for offline users after Send().
    /// </summary>
    public async Task SendEmail(string toEmail, string subject, string body)
    {
        var emailConfig = _config.GetSection("Email");
        var smtpHost = emailConfig["SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.TryParse(emailConfig["SmtpPort"], out var p) ? p : 587;
        var smtpUser = emailConfig["SmtpUser"] ?? string.Empty;
        var smtpPass = emailConfig["SmtpPassword"] ?? string.Empty;
        var fromEmail = emailConfig["FromEmail"] ?? "noreply@connecthub.io";
        var fromName = emailConfig["FromName"] ?? "ConnectHub";

        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(fromName, fromEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(smtpUser, smtpPass);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("[Email] Sent to {Email} Subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Failed to send to {Email}", toEmail);
        }
    }

    public async Task<IList<NotificationDto>> GetAll(int page = 1, int pageSize = 20)
    {
        var notifications = await _repo.GetAll(page, pageSize);
        return notifications.Select(MapToDto).ToList();
    }

    private static NotificationDto MapToDto(Models.Entities.Notification n) => new()
    {
        NotificationId = n.NotificationId,
        RecipientId = n.RecipientId,
        SenderId = n.SenderId,
        Type = n.Type,
        Title = n.Title,
        Message = n.Message,
        RelatedId = n.RelatedId,
        RelatedType = n.RelatedType,
        IsRead = n.IsRead,
        SentAt = n.SentAt
    };
}
