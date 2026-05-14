using System.Security.Claims;
using ConnectHub.ChatRoom.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectHub.ChatRoom.Hubs;

/// <summary>
/// RoomHub — ASP.NET Core SignalR Hub for ConnectHub ChatRoom real-time events.
/// JWT passed via ?access_token= for WebSocket transport.
///
/// Responsible for:
///   JoinRoom  → Groups.AddToGroupAsync — user joins SignalR group for room
///   LeaveRoom → Groups.RemoveFromGroupAsync — user leaves SignalR group
///   Broadcasts: UserJoinedRoom, UserLeftRoom, RoomUpdated, MemberRoleChanged, MemberRemoved
///
/// Note: actual room MESSAGES are sent via UC2 ChatHub (/hubs/chat).
/// This hub manages room MEMBERSHIP events and PRESENCE within rooms.
///
/// FLOW for Join:
///   1. Client calls JoinRoom(roomId)
///   2. Hub calls _roomService.JoinRoom() → saves to DB → publishes RabbitMQ event
///   3. Hub calls Groups.AddToGroupAsync — now receives room broadcasts
///   4. Hub broadcasts UserJoinedRoom to all room members
///
/// PERFORMANCE NOTE: OnConnectedAsync does NOT query DB.
///   Client is responsible for calling JoinRoom(roomId) for each room after connecting.
///   This avoids an expensive DB query per-connection and scales correctly.
/// </summary>
[Authorize]
public class RoomHub : Hub
{
    private readonly IChatRoomService _roomService;
    private readonly ILogger<RoomHub> _logger;

    public RoomHub(IChatRoomService roomService, ILogger<RoomHub> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    // ── Connection lifecycle ──────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromContext();
        _logger.LogInformation("User {UserId} connected to RoomHub. ConnectionId={ConnectionId}",
            userId, Context.ConnectionId);

        //NO DB query here — avoids expensive per-connection room lookup.
        // Client calls JoinRoom(roomId) explicitly for each room it needs to subscribe to.
        // This is the correct pattern for SignalR at scale.
        await Clients.Others.SendAsync("UserConnected", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();
        _logger.LogInformation("User {UserId} disconnected from RoomHub.", userId);

        await Clients.Others.SendAsync("UserDisconnected", userId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Room membership ───────────────────────────────────────────

    /// <summary>
    /// JoinRoom:
    ///   1. Validates room access
    ///   2. DB + RabbitMQ via service (skipped if already a member)
    ///   3. Groups.AddToGroupAsync — receives future broadcasts
    ///   4. Notifies room group
    /// </summary>
    public async Task JoinRoom(int roomId)
    {
        var userId = GetUserIdFromContext();
        var userName = GetUserNameFromContext();

        var alreadyMember = await _roomService.IsMember(roomId, userId);

        if (!alreadyMember)
            await _roomService.JoinRoom(roomId, userId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"room-{roomId}");

        await Clients.Group($"room-{roomId}")
            .SendAsync("UserJoinedRoom", new { userId, userName, roomId });

        _logger.LogInformation("User {UserId} joined room {RoomId} via SignalR", userId, roomId);
    }

    /// <summary>
    /// LeaveRoom:
    ///   1. DB + RabbitMQ via service
    ///   2. Groups.RemoveFromGroupAsync
    ///   3. Notifies room group
    /// </summary>
    public async Task LeaveRoom(int roomId)
    {
        var userId = GetUserIdFromContext();
        var userName = GetUserNameFromContext();

        await _roomService.LeaveRoom(roomId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room-{roomId}");

        await Clients.Group($"room-{roomId}")
            .SendAsync("UserLeftRoom", new { userId, userName, roomId });

        _logger.LogInformation("User {UserId} left room {RoomId} via SignalR", userId, roomId);
    }

    /// <summary>
    /// Notify all room members when room details are updated (name, description).
    /// Called by RoomController after UpdateRoom — broadcasts to SignalR group.
    /// </summary>
    public async Task NotifyRoomUpdated(int roomId, string name, string? description)
    {
        await Clients.Group($"room-{roomId}")
            .SendAsync("RoomUpdated", new { roomId, name, description });
    }

    /// <summary>
    /// Notify room group when a member's role changes.
    /// </summary>
    public async Task NotifyMemberRoleChanged(int roomId, int userId, string newRole)
    {
        await Clients.Group($"room-{roomId}")
            .SendAsync("MemberRoleChanged", new { roomId, userId, newRole });
    }

    /// <summary>
    /// Notify room group when a member is removed by admin/moderator.
    /// </summary>
    public async Task NotifyMemberRemoved(int roomId, int removedUserId)
    {
        await Clients.Group($"room-{roomId}")
            .SendAsync("MemberRemoved", new { roomId, removedUserId });
    }

    // ── Typing in room ────────────────────────────────────────────

    /// <summary>
    /// Typing indicator for room — broadcasts to OthersInGroup.
    /// Complements UC2 ChatHub.TypingIndicatorRoom.
    /// </summary>
    public async Task TypingInRoom(int roomId, bool isTyping)
    {
        var userId = GetUserIdFromContext();
        var userName = GetUserNameFromContext();

        await Clients.OthersInGroup($"room-{roomId}")
            .SendAsync("RoomTypingIndicator", new { roomId, userId, userName, isTyping });
    }

    // ── Helpers ───────────────────────────────────────────────────

    private int GetUserIdFromContext()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (claim is null || !int.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("Unable to determine user identity.");

        return userId;
    }

    private string GetUserNameFromContext() =>
        Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
}
