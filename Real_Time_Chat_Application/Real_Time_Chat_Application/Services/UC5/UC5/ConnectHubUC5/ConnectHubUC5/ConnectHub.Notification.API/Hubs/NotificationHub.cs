using System.Security.Claims;
using ConnectHub.Notification.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectHub.Notification.Hubs;

/// <summary>
/// NotificationHub — SignalR Hub for real-time notification badge updates.
/// JWT passed via ?access_token= for WebSocket transport.
///
/// As per class diagram (Figure 6):
///   After Send(), calls IHubContext&lt;NotificationHub&gt;.Clients.User(recipientId)
///   .SendAsync("NotificationCount", unreadCount) for real-time badge update.
///
/// IHubContext&lt;NotificationHub&gt; is injected into NotificationService so the
/// service can push to clients without requiring an active hub connection from
/// the sender — it pushes to the RECIPIENT's active connections.
///
/// SignalR events pushed to clients:
///   NotificationCount → { unreadCount } — badge count update
///   NewNotification   → NotificationDto — full notification payload
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromContext();

        // Push current unread count to client on initial connection
        var unreadCount = await _notificationService.GetUnreadCount(userId);
        await Clients.Caller.SendAsync("NotificationCount", unreadCount);

        _logger.LogInformation(
            "User {UserId} connected to NotificationHub. UnreadCount={Count}",
            userId, unreadCount);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();
        _logger.LogInformation("User {UserId} disconnected from NotificationHub.", userId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// GetUnreadCount — client calls to refresh badge count.
    /// </summary>
    public async Task GetUnreadCount()
    {
        var userId = GetUserIdFromContext();
        var count = await _notificationService.GetUnreadCount(userId);
        await Clients.Caller.SendAsync("NotificationCount", count);
    }

    /// <summary>
    /// MarkAsRead — mark a single notification as read via SignalR.
    /// Updates badge count and pushes updated count to caller.
    /// </summary>
    public async Task MarkAsRead(int notificationId)
    {
        var userId = GetUserIdFromContext();
        await _notificationService.MarkAsRead(notificationId);

        // Push updated badge count
        var count = await _notificationService.GetUnreadCount(userId);
        await Clients.Caller.SendAsync("NotificationCount", count);
    }

    /// <summary>
    /// MarkAllRead — mark all notifications as read via SignalR.
    /// Resets badge count to 0.
    /// </summary>
    public async Task MarkAllRead()
    {
        var userId = GetUserIdFromContext();
        await _notificationService.MarkAllRead(userId);
        await Clients.Caller.SendAsync("NotificationCount", 0);
    }

    private int GetUserIdFromContext()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (claim is null || !int.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("Unable to determine user identity.");

        return userId;
    }
}
