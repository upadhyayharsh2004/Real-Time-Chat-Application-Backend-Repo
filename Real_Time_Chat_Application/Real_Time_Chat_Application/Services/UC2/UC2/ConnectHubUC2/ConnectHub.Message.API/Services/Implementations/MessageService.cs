using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Models.Entities;
using ConnectHub.Message.Models.Events;
using ConnectHub.Message.Repositories.Interfaces;
using ConnectHub.Message.Services.Interfaces;

namespace ConnectHub.Message.Services.Implementations;

/// <summary>
/// MessageService — FIXED implementation of IMessageService.
///
/// CRITICAL FIX: Original UC2 had NO RabbitMQ in MessageService at all.
/// The Hub published to RabbitMQ before DB save (no MessageId), and the
/// consumer tried to save with no MessageId — broken circular flow.
///
/// CORRECT FLOW:
///   1. Save to SQL Server via EF Core → get valid MessageId
///   2. Publish typed event to RabbitMQ AFTER save (MessageId is now valid)
///   3. ChatHub then broadcasts via SignalR (Hub calls service, gets back DTO)
///
/// This guarantees MessageId consistency across DB, RabbitMQ, and SignalR.
/// </summary>
public class MessageService : IMessageService
{
    private readonly IMessageRepository _repo;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository repo,
        IRabbitMqPublisher publisher,
        ILogger<MessageService> logger)
    {
        _repo = repo;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<MessageDto> SendMessage(Models.Entities.Message message, string senderName = "")
    {
        if (message.ReceiverId == null && message.RoomId == null)
            throw new ArgumentException("Either ReceiverId or RoomId must be set.");

        if (message.ReceiverId != null && message.RoomId != null)
            throw new ArgumentException("Only one of ReceiverId or RoomId can be set.");

        message.SentAt = DateTime.UtcNow;
        message.IsRead = false;
        message.IsDeleted = false;
        message.IsEdited = false;

        // STEP 1: Save to DB → get valid MessageId
        var saved = await _repo.AddMessage(message);
        _logger.LogInformation("Message {MessageId} saved by {SenderId}", saved.MessageId, saved.SenderId);

        // STEP 2: Publish to RabbitMQ AFTER DB save — MessageId is now valid
        if (saved.ReceiverId.HasValue)
        {
            await _publisher.PublishMessageSentAsync(new MessageSentEvent
            {
                MessageId = saved.MessageId,
                SenderId = saved.SenderId,
                SenderName = senderName,
                ReceiverId = saved.ReceiverId.Value,
                Content = saved.Content,
                MessageType = saved.MessageType,
                MediaUrl = saved.MediaUrl,
                SentAt = saved.SentAt
            });
        }
        else if (saved.RoomId.HasValue)
        {
            // await _publisher.PublishRoomMessageSentAsync(new RoomMessageSentEvent
            // {
            //     MessageId = saved.MessageId,
            //     SenderId = saved.SenderId,
            //     SenderName = senderName,
            //     RoomId = saved.RoomId.Value,
            //     Content = saved.Content,
            //     MessageType = saved.MessageType,
            //     MediaUrl = saved.MediaUrl,
            //     SentAt = saved.SentAt,
            //     MentionedUserNames = ParseMentions(saved.Content)
            // });
        }

        // ✅ STEP 3: Return DTO — Hub will then broadcast via SignalR
        return MapToDto(saved);
    }

    public async Task<MessageDto?> GetMessageById(int messageId)
    {
        var message = await _repo.FindByMessageId(messageId);
        return message is null ? null : MapToDto(message);
    }

    public async Task<IList<MessageDto>> GetDirectMessages(int senderId, int receiverId)
    {
        var messages = await _repo.FindBySenderAndReceiver(senderId, receiverId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task<IList<MessageDto>> GetRoomMessages(int roomId)
    {
        var messages = await _repo.FindByRoomId(roomId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task<IList<MessageDto>> GetUnreadMessages(int userId)
    {
        var messages = await _repo.FindUnreadByReceiverId(userId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task MarkAsRead(int messageId, int receiverId)
    {
        var message = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        var marked = await _repo.MarkMessageRead(messageId, receiverId);
        if (!marked)
            throw new KeyNotFoundException($"Message {messageId} not found or not addressed to receiver {receiverId}.");

        _logger.LogInformation("Message {MessageId} marked as read by {ReceiverId}", messageId, receiverId);

        // Publish read receipt event — Notification-Service clears badge count
        await _publisher.PublishMessageReadAsync(new MessageReadEvent
        {
            MessageId = messageId,
            SenderId = message.SenderId,
            ReadByUserId = receiverId,
            ReadAt = DateTime.UtcNow
        });
    }

    public async Task MarkAllAsRead(int senderId, int receiverId)
    {
        await _repo.MarkAllReadBySender(senderId, receiverId);
        _logger.LogInformation("All messages from {SenderId} to {ReceiverId} marked as read", senderId, receiverId);
    }

    public async Task<MessageDto> EditMessage(int messageId, string newContent, int requestingUserId)
    {
        var message = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        if (message.IsDeleted)
            throw new InvalidOperationException("Cannot edit a deleted message.");

        if (message.SenderId != requestingUserId)
            throw new UnauthorizedAccessException("You can only edit your own messages.");

        message.Content = newContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        var updated = await _repo.UpdateMessage(message);
        _logger.LogInformation("Message {MessageId} edited by {UserId}", messageId, requestingUserId);

        await _publisher.PublishMessageEditedAsync(new MessageEditedEvent
        {
            MessageId = messageId,
            EditedByUserId = requestingUserId,
            NewContent = newContent,
            EditedAt = updated.EditedAt ?? DateTime.UtcNow
        });

        return MapToDto(updated);
    }

    public async Task DeleteMessage(int messageId, int requestingUserId, string? requestingUserRole = null)
    {
        var message = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        bool isAdmin = requestingUserRole == "Admin";
        if (!isAdmin && message.SenderId != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

        await _repo.DeleteByMessageId(messageId);
        _logger.LogInformation("Message {MessageId} soft-deleted by {UserId} (Admin={IsAdmin})",
            messageId, requestingUserId, isAdmin);

        await _publisher.PublishMessageDeletedAsync(new MessageDeletedEvent
        {
            MessageId = messageId,
            DeletedByUserId = requestingUserId,
            IsAdminDelete = isAdmin,
            DeletedAt = DateTime.UtcNow
        });
    }

    public async Task<int> GetUnreadCount(int userId) =>
        await _repo.CountUnreadByReceiverId(userId);

    public async Task<IList<RecentChatDto>> GetRecentChats(int userId)
    {
        var recentMessages = await _repo.GetRecentChats(userId);

        var result = new List<RecentChatDto>();

        // distinct partners only
        var partnerIds = recentMessages
            .Select(m => m.SenderId == userId
                ? (m.ReceiverId ?? 0)
                : m.SenderId)
            .Distinct()
            .ToList();

        foreach (var partnerId in partnerIds)
        {
            var lastMessage = recentMessages
                .Where(m =>
                    (m.SenderId == userId && m.ReceiverId == partnerId) ||
                    (m.SenderId == partnerId && m.ReceiverId == userId))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            // Deleted skip karke previous lo
            var displayMessage = recentMessages
                .Where(m =>
                    ((m.SenderId == userId && m.ReceiverId == partnerId) ||
                    (m.SenderId == partnerId && m.ReceiverId == userId))
                    && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            if (lastMessage == null) continue; // koi bhi message nahi hai toh skip

            int unreadCount = await _repo.CountUnreadFromSender(userId, partnerId);

            result.Add(new RecentChatDto
            {
                UserId = partnerId,
                UserName = $"user_{partnerId}",
                DisplayName = $"User {partnerId}",
                AvatarUrl = null,
                LastMessage = displayMessage?.Content ?? "",  // deleted nahi wala dikhao
                LastMessageAt = lastMessage.SentAt,
                UnreadCount = unreadCount,
                IsOnline = false
            });
        }

        return result
            .OrderByDescending(x => x.LastMessageAt)
            .ToList();
    }

    public async Task<IList<MessageDto>> SearchMessages(string query, int userId)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<MessageDto>();
        var messages = await _repo.SearchMessages(query, userId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task<IList<MessageDto>> GetMessagesByRoom(int roomId)
    {
        var messages = await _repo.FindByRoomId(roomId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task<PagedMessagesDto> GetDirectMessagesPaged(
        int senderId, int receiverId, int page = 1, int pageSize = 20)
    {
        var (messages, total) = await _repo.GetDirectMessagesPaged(senderId, receiverId, page, pageSize);
        return new PagedMessagesDto
        {
            Messages = messages.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < total
        };
    }

    public async Task<PagedMessagesDto> GetRoomMessagesPaged(
        int roomId, int page = 1, int pageSize = 20)
    {
        var (messages, total) = await _repo.GetRoomMessagesPaged(roomId, page, pageSize);
        return new PagedMessagesDto
        {
            Messages = messages.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < total
        };
    }

    public async Task<int> GetUnreadCountFromSender(int receiverId, int senderId) =>
        await _repo.CountUnreadFromSender(receiverId, senderId);

    public async Task<ReactionDto> AddReaction(int messageId, int userId, string emoji)
    {
        var message = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        if (message.IsDeleted)
            throw new InvalidOperationException("Cannot react to a deleted message.");

        // Check DB directly — never trust the loaded Reactions collection (stale cache)
        var removed = await _repo.RemoveReaction(messageId, userId, emoji);
        if (removed)
        {
            // Already existed → toggled off
            return new ReactionDto
            {
                ReactionId = -1,
                UserId = userId,
                Emoji = emoji,
                ReactedAt = DateTime.UtcNow
            };
        }

        // Didn't exist → add it
        var reaction = new MessageReaction
        {
            MessageId = messageId,
            UserId = userId,
            Emoji = emoji,
            ReactedAt = DateTime.UtcNow
        };

        var saved = await _repo.AddReaction(reaction);
        return new ReactionDto
        {
            ReactionId = saved.ReactionId,
            UserId = saved.UserId,
            Emoji = saved.Emoji,
            ReactedAt = saved.ReactedAt
        };
    }

    public async Task<bool> RemoveReaction(int messageId, int userId, string emoji) =>
        await _repo.RemoveReaction(messageId, userId, emoji);

    public async Task<IList<ReactionDto>> GetReactions(int messageId)
    {
        var reactions = await _repo.GetReactions(messageId);
        return reactions.Select(r => new ReactionDto
        {
            ReactionId = r.ReactionId,
            UserId = r.UserId,
            Emoji = r.Emoji,
            ReactedAt = r.ReactedAt
        }).ToList();
    }

    public async Task<PinnedMessageDto> PinMessage(
        int messageId, int pinnedByUserId, int? conversationWithUserId, int? roomId)
    {
        var message = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        var pin = new ConversationPin
        {
            MessageId = messageId,
            PinnedByUserId = pinnedByUserId,
            ConversationWithUserId = conversationWithUserId,
            RoomId = roomId,
            PinnedAt = DateTime.UtcNow
        };

        var saved = await _repo.PinMessage(pin);
        return new PinnedMessageDto
        {
            PinId = saved.PinId,
            Message = MapToDto(message),
            PinnedByUserId = saved.PinnedByUserId,
            PinnedAt = saved.PinnedAt
        };
    }

    public async Task<bool> UnpinMessage(int pinId, int userId) =>
        await _repo.UnpinMessage(pinId, userId);

    public async Task<IList<PinnedMessageDto>> GetPinnedMessages(int? userId, int? roomId)
    {
        var pins = await _repo.GetPinnedMessages(userId, roomId);
        return pins.Select(p => new PinnedMessageDto
        {
            PinId = p.PinId,
            Message = MapToDto(p.Message),
            PinnedByUserId = p.PinnedByUserId,
            PinnedAt = p.PinnedAt
        }).ToList();
    }

    public async Task AdminDeleteMessage(int messageId)
    {
        _ = await _repo.FindByMessageId(messageId)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        await _repo.DeleteByMessageId(messageId);
        _logger.LogWarning("Admin soft-deleted message {MessageId}", messageId);

        await _publisher.PublishMessageDeletedAsync(new MessageDeletedEvent
        {
            MessageId = messageId,
            DeletedByUserId = 0,
            IsAdminDelete = true,
            DeletedAt = DateTime.UtcNow
        });
    }

    public async Task<IList<MessageDto>> AdminGetDirectMessages(int senderId, int receiverId)
    {
        var messages = await _repo.FindBySenderAndReceiver(senderId, receiverId);
        return messages.Select(MapToDto).ToList();
    }

    public async Task<IList<MessageDto>> AdminGetRoomMessages(int roomId)
    {
        var messages = await _repo.FindByRoomId(roomId);
        return messages.Select(MapToDto).ToList();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static List<string> ParseMentions(string content) =>
        content.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.StartsWith('@') && w.Length > 1)
            .Select(w => w[1..].TrimEnd('.', ',', '!', '?'))
            .Distinct().ToList();

    // private static MessageDto MapToDto(Models.Entities.Message m) => new()
    // {
    //     MessageId = m.MessageId,
    //     SenderId = m.SenderId,
    //     SenderName = string.Empty,
    //     SenderAvatarUrl = null,
    //     ReceiverId = m.ReceiverId,
    //     RoomId = m.RoomId,
    //     Content = m.IsDeleted ? "[Message deleted]" : m.Content,
    //     MessageType = m.MessageType,
    //     IsRead = m.IsRead,
    //     IsDeleted = m.IsDeleted,
    //     IsEdited = m.IsEdited,
    //     SentAt = m.SentAt,
    //     ReadAt = m.ReadAt,
    //     EditedAt = m.EditedAt,
    //     MediaUrl = m.MediaUrl,
    //     ReplyToMessageId = m.ReplyToMessageId,
    //     ReplyToMessage = m.ReplyToMessage is null ? null : MapToDto(m.ReplyToMessage),
    //     Reactions = m.Reactions.Select(r => new ReactionDto
    //     {
    //         ReactionId = r.ReactionId,
    //         UserId = r.UserId,
    //         Emoji = r.Emoji,
    //         ReactedAt = r.ReactedAt
    //     }).ToList()
    // };
    private static MessageDto MapToDto(Models.Entities.Message m) => new()
    {
        MessageId = m.MessageId,
        SenderId = m.SenderId,
        SenderName = string.Empty,
        SenderAvatarUrl = null,
        ReceiverId = m.ReceiverId,
        RoomId = m.RoomId,
        Content = m.IsDeleted ? "[Message deleted]" : m.Content,
        MessageType = m.MessageType,
        IsRead = m.IsRead,
        IsDeleted = m.IsDeleted,
        IsEdited = m.IsEdited,
        SentAt = m.SentAt,
        ReadAt = m.ReadAt,
        EditedAt = m.EditedAt,
        MediaUrl = m.MediaUrl,
        ReplyToMessageId = m.ReplyToMessageId,
        // FIX 1: Null check for ReplyToMessage
        ReplyToMessage = m.ReplyToMessage == null ? null : MapToDto(m.ReplyToMessage),
        // FIX 2: Null check for Reactions
        Reactions = m.Reactions?.Select(r => new ReactionDto
        {
            ReactionId = r.ReactionId,
            UserId = r.UserId,
            Emoji = r.Emoji,
            ReactedAt = r.ReactedAt
        }).ToList() ?? new List<ReactionDto>() // Agar null hai toh empty list bhej do
    };
}