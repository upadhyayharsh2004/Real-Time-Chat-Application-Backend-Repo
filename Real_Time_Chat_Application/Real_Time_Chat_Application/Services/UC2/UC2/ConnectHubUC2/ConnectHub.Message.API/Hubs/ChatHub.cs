
using System.Security.Claims;
using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectHub.Message.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMessageService messageService, ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromContext();
        _logger.LogInformation("User {UserId} connected. ConnectionId={ConnectionId}", userId, Context.ConnectionId);
        await Clients.Others.SendAsync("UserConnected", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();
        _logger.LogInformation("User {UserId} disconnected. ConnectionId={ConnectionId}", userId, Context.ConnectionId);
        await Clients.Others.SendAsync("UserDisconnected", userId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendDirectMessage(int receiverId, string content, string messageType, string? mediaUrl)
    {
        try
        {
            var senderId = GetUserIdFromContext();
            var senderName = GetUserNameFromContext();

            _logger.LogInformation("[ChatHub] SendDirectMessage START: From {SenderId} to {ReceiverId}", senderId, receiverId);

            var message = new Models.Entities.Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                MessageType = messageType ?? "TEXT",
                MediaUrl = mediaUrl,
                SentAt = DateTime.UtcNow
            };

            var saved = await _messageService.SendMessage(message, senderName);
            saved.SenderName = senderName;

            await Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", saved);
            await Clients.Caller.SendAsync("MessageSent", saved);

            _logger.LogInformation("[ChatHub] SendDirectMessage SUCCESS: MsgId {MessageId}", saved.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChatHub] SendDirectMessage FAILED: {Error}", ex.Message);
            throw;
        }
    }

    // public async Task SendRoomMessage(
    // int roomId, string content,
    // string messageType = "TEXT", string? mediaUrl = null, int? replyToMessageId = null)
    // {
    //     try  // ← YEH ADD KARO
    //     {
    //         var senderId = GetUserIdFromContext();
    //         var senderName = GetUserNameFromContext();

    //         _logger.LogInformation("[ChatHub] SendRoomMessage START: From {SenderId} to Room {RoomId}", senderId, roomId);

    //         var message = new Models.Entities.Message
    //         {
    //             SenderId = senderId,
    //             RoomId = roomId,
    //             Content = content,
    //             MessageType = messageType,
    //             MediaUrl = mediaUrl,
    //             ReplyToMessageId = replyToMessageId,
    //             SentAt = DateTime.UtcNow
    //         };

    //         var saved = await _messageService.SendMessage(message, senderName);
    //         saved.SenderName = senderName;

    //         await Clients.Group(roomId.ToString()).SendAsync("ReceiveRoomMessage", saved);

    //         _logger.LogInformation("[ChatHub] SendRoomMessage SUCCESS: MsgId {MessageId} by {SenderId} in Room {RoomId}", saved.MessageId, senderId, roomId);
    //     }
    //     catch (Exception ex)  // ← YEH ADD KARO
    //     {
    //         _logger.LogError(ex, "[ChatHub] SendRoomMessage FAILED: RoomId={RoomId}, Error={Error}", roomId, ex.Message);
    //         throw;
    //     }
    // }

    public async Task SendRoomMessage(int roomId, string content, string messageType, string? mediaUrl)
    {
        // YEH LOG TERMINAL MEIN JARUR DIKHNA CHAHIYE
        _logger.LogWarning("!!! SERVER HIT !!! RoomId: {RoomId}, Content: {Content}", roomId, content);

        try
        {
            var senderId = GetUserIdFromContext();
            var message = new Models.Entities.Message
            {
                SenderId = senderId,
                RoomId = roomId,
                Content = content,
                MessageType = messageType ?? "TEXT",
                MediaUrl = mediaUrl,
                SentAt = DateTime.UtcNow
            };

            var saved = await _messageService.SendMessage(message, "User");
            await Clients.Group(roomId.ToString()).SendAsync("ReceiveRoomMessage", saved);
            _logger.LogInformation("!!! MESSAGE SENT SUCCESS !!! MsgId: {MessageId}", saved.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! SERVER ERROR !!! " + ex.Message);
            throw;
        }
    }


    public async Task JoinRoom(int roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        var userId = GetUserIdFromContext();
        var userName = GetUserNameFromContext();
        await Clients.Group(roomId.ToString()).SendAsync("UserJoinedRoom", new { userId, userName, roomId });
        _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
        var userId = GetUserIdFromContext();
        var userName = GetUserNameFromContext();
        await Clients.Group(roomId.ToString()).SendAsync("UserLeftRoom", new { userId, userName, roomId });
        _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
    }

    public async Task TypingIndicator(int recipientId, bool isTyping)
    {
        var senderId = GetUserIdFromContext();
        var senderName = GetUserNameFromContext();
        await Clients.User(recipientId.ToString()).SendAsync("TypingIndicator", new { senderId, senderName, isTyping });
    }

    public async Task TypingIndicatorRoom(int roomId, bool isTyping)
    {
        var senderId = GetUserIdFromContext();
        var senderName = GetUserNameFromContext();
        await Clients.OthersInGroup(roomId.ToString()).SendAsync("RoomTypingIndicator", new { roomId, senderId, senderName, isTyping });
    }

    public async Task MarkMessageRead(int messageId)
    {
        var receiverId = GetUserIdFromContext();
        var message = await _messageService.GetMessageById(messageId);
        if (message is null) return;
        await _messageService.MarkAsRead(messageId, receiverId);
        await Clients.User(message.SenderId.ToString()).SendAsync("MessageRead", new
        {
            messageId,
            readBy = receiverId,
            readAt = DateTime.UtcNow
        });
    }

    public async Task ReactToMessage(int messageId, string emoji)
    {
        var userId = GetUserIdFromContext();
        var reaction = await _messageService.AddReaction(messageId, userId, emoji);
        var message = await _messageService.GetMessageById(messageId);
        if (message is null) return;

        var reactionEvent = new { messageId, userId, emoji, reactionId = reaction.ReactionId, reactedAt = reaction.ReactedAt };

        if (message.ReceiverId.HasValue)
        {
            await Clients.User(message.SenderId.ToString()).SendAsync("ReactionAdded", reactionEvent);
            await Clients.User(message.ReceiverId.Value.ToString()).SendAsync("ReactionAdded", reactionEvent);
        }
        else if (message.RoomId.HasValue)
        {
            await Clients.Group(message.RoomId.Value.ToString()).SendAsync("ReactionAdded", reactionEvent);
        }
    }

    public async Task RemoveReactionFromMessage(int messageId, string emoji)
    {
        var userId = GetUserIdFromContext();
        var removed = await _messageService.RemoveReaction(messageId, userId, emoji);
        if (!removed) return;

        var message = await _messageService.GetMessageById(messageId);
        if (message is null) return;

        var reactionEvent = new { messageId, userId, emoji };

        if (message.ReceiverId.HasValue)
        {
            await Clients.User(message.SenderId.ToString()).SendAsync("ReactionRemoved", reactionEvent);
            await Clients.User(message.ReceiverId.Value.ToString()).SendAsync("ReactionRemoved", reactionEvent);
        }
        else if (message.RoomId.HasValue)
        {
            await Clients.Group(message.RoomId.Value.ToString()).SendAsync("ReactionRemoved", reactionEvent);
        }
    }

    // Delete for Everyone — dono ke liye hatega, DB mein bhi delete hoga
    public async Task DeleteMessage(int messageId)
    {
        var userId = GetUserIdFromContext();
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        var message = await _messageService.GetMessageById(messageId);
        if (message is null) return;

        await _messageService.DeleteMessage(messageId, userId, role);

        if (message.ReceiverId.HasValue)
        {
            await Clients.User(message.SenderId.ToString()).SendAsync("MessageDeleted", messageId);
            await Clients.User(message.ReceiverId.Value.ToString()).SendAsync("MessageDeleted", messageId);
        }
        else if (message.RoomId.HasValue)
        {
            await Clients.Group(message.RoomId.Value.ToString()).SendAsync("MessageDeleted", messageId);
        }
    }

    // Delete for Me — alag event bhejta hai taaki frontend localStorage mein save kare
    public async Task DeleteMessageForMe(int messageId)
    {
        await Clients.Caller.SendAsync("MessageDeletedForMe", messageId);
    }

    public async Task EditMessage(int messageId, string newContent)
    {
        var userId = GetUserIdFromContext();
        var updated = await _messageService.EditMessage(messageId, newContent, userId);

        var editEvent = new
        {
            messageId = updated.MessageId,
            content = updated.Content,
            isEdited = true,
            editedAt = updated.EditedAt,
            senderId = updated.SenderId,
            receiverId = updated.ReceiverId,
            roomId = updated.RoomId,
        };

        if (updated.ReceiverId.HasValue)
        {
            await Clients.User(updated.SenderId.ToString()).SendAsync("MessageEdited", editEvent);
            await Clients.User(updated.ReceiverId.Value.ToString()).SendAsync("MessageEdited", editEvent);
        }
        else if (updated.RoomId.HasValue)
        {
            await Clients.Group(updated.RoomId.Value.ToString()).SendAsync("MessageEdited", editEvent);
        }
    }

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