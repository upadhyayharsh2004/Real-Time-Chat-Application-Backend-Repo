using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Models.Entities;

namespace ConnectHub.Message.Services.Interfaces;

/// <summary>
/// IMessageService — ConnectHub Message Services
/// Methods as specified in the Message-Service Class Diagram (Figure 3)
/// </summary>
public interface IMessageService
{
    // ── Core message operations (from class diagram) ───────────────

    Task<MessageDto> SendMessage(Models.Entities.Message message, string senderName = "");

    Task<MessageDto?> GetMessageById(int messageId);

    Task<IList<MessageDto>> GetDirectMessages(int senderId, int receiverId);

    Task<IList<MessageDto>> GetRoomMessages(int roomId);

    Task<IList<MessageDto>> GetUnreadMessages(int userId);

    Task MarkAsRead(int messageId, int receiverId);

    Task MarkAllAsRead(int senderId, int receiverId);

    Task<MessageDto> EditMessage(int messageId, string newContent, int requestingUserId);

    Task DeleteMessage(int messageId, int requestingUserId, string? requestingUserRole = null);

    Task<int> GetUnreadCount(int userId);

    Task<IList<RecentChatDto>> GetRecentChats(int userId);

    Task<IList<MessageDto>> SearchMessages(string query, int userId);

    Task<IList<MessageDto>> GetMessagesByRoom(int roomId);

    // ── Extended (extra beyond spec) ───────────────────────────────

    /// <summary>
    /// Paginated direct messages — GET /api/messages/direct/{userId}?page=1&amp;pageSize=20
    /// </summary>
    Task<PagedMessagesDto> GetDirectMessagesPaged(
        int senderId, int receiverId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Paginated room messages — GET /api/messages/room/{roomId}?page=1&amp;pageSize=20
    /// </summary>
    Task<PagedMessagesDto> GetRoomMessagesPaged(
        int roomId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Count unread messages specifically from one sender
    /// </summary>
    Task<int> GetUnreadCountFromSender(int receiverId, int senderId);

    // ── Reactions ──────────────────────────────────────────────────
    Task<ReactionDto> AddReaction(int messageId, int userId, string emoji);
    Task<bool> RemoveReaction(int messageId, int userId, string emoji);
    Task<IList<ReactionDto>> GetReactions(int messageId);

    // ── Pins ───────────────────────────────────────────────────────
    Task<PinnedMessageDto> PinMessage(int messageId, int pinnedByUserId, int? conversationWithUserId, int? roomId);
    Task<bool> UnpinMessage(int pinId, int userId);
    Task<IList<PinnedMessageDto>> GetPinnedMessages(int? userId, int? roomId);

    // ── Admin operations ───────────────────────────────────────────
    /// <summary>
    /// Admin can delete any message from any user
    /// DELETE /api/messages/{id} — Admin role
    /// </summary>
    Task AdminDeleteMessage(int messageId);

    /// <summary>
    /// Admin can view all direct messages between two users
    /// GET /api/admin/messages/direct?senderId=&amp;receiverId=
    /// </summary>
    Task<IList<MessageDto>> AdminGetDirectMessages(int senderId, int receiverId);

    /// <summary>
    /// Admin get all messages in a room
    /// </summary>
    Task<IList<MessageDto>> AdminGetRoomMessages(int roomId);
}