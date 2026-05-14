using System.Security.Claims;
using ConnectHub.Presence.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectHub.Presence.Hubs;

/// <summary>
/// PresenceHub — SignalR Hub for real-time online/offline status.
/// JWT via ?access_token= query string (same pattern as UC2 ChatHub + UC3 RoomHub).
///
/// OnConnectedAsync:
///   1. Calls IPresenceService.UserConnected(userId, connectionId) — sync, updates ConcurrentDictionary
///      DB persisted async (fire-and-forget). RabbitMQ published if first connection.
///   2. Broadcasts "UserOnline" to all OTHER connected clients
///
/// OnDisconnectedAsync:
///   1. Calls IPresenceService.UserDisconnected(userId, connectionId) — sync
///      If last connection: DB offline + RabbitMQ UserPresenceOfflineEvent published
///   2. Broadcasts "UserOffline" to all OTHER clients (only if truly offline)
///
/// Hub methods:
///   GetOnlineUsers()    — returns current online list (ConcurrentDictionary, no DB)
///   GetOnlineUsersInfo()— returns full UserConnection metadata
///   Ping()              — client heartbeat, updates LastActiveAt in DB
/// </summary>
[Authorize]
public class PresenceHub : Hub
{
    private readonly IPresenceService _presenceService;
    private readonly ILogger<PresenceHub> _logger;

    public PresenceHub(IPresenceService presenceService, ILogger<PresenceHub> logger)
    {
        _presenceService = presenceService;
        _logger          = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId     = GetUserIdFromContext();
        var deviceInfo = Context.GetHttpContext()
            ?.Request.Headers["User-Agent"].ToString();

        // Sync: updates ConcurrentDictionary, fires DB + RabbitMQ async
        _presenceService.UserConnected(userId, Context.ConnectionId, deviceInfo);

        // Broadcast online to all OTHER clients
        await Clients.Others.SendAsync("UserOnline", new
        {
            userId,
            connectedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "User {UserId} connected to PresenceHub. ConnectionId={ConnId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();

        // Sync: removes from ConcurrentDictionary, fires DB + RabbitMQ async if last conn
        _presenceService.UserDisconnected(userId, Context.ConnectionId);

        // Only broadcast offline if truly offline (no remaining connections)
        if (!_presenceService.IsUserOnline(userId))
        {
            await Clients.Others.SendAsync("UserOffline", new
            {
                userId,
                lastSeen = DateTime.UtcNow
            });
        }

        _logger.LogInformation(
            "User {UserId} disconnected. StillOnline={IsOnline}",
            userId, _presenceService.IsUserOnline(userId));

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client calls on load to get current online user list. No DB round-trip.</summary>
    public async Task GetOnlineUsers()
    {
        var onlineUsers = await _presenceService.GetOnlineUsersDtos();
        await Clients.Caller.SendAsync("OnlineUsersList", onlineUsers);
    }

    /// <summary>Returns full UserConnection metadata (connectionId, deviceInfo, connectedAt).</summary>
    public async Task GetOnlineUsersInfo()
    {
        var info = _presenceService.GetOnlineUsersInfo();
        await Clients.Caller.SendAsync("OnlineUsersInfo", info);
    }

    /// <summary>Client heartbeat every 30s — updates LastActiveAt in DB.</summary>
    public async Task Ping()
    {
        var userId = GetUserIdFromContext();
        await _presenceService.UpdateLastActiveAt(userId);
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
