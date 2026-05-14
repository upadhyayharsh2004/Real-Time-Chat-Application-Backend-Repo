using ConnectHub.Message.Models.Entities;

namespace ConnectHub.Message.Repositories.Interfaces;

/// <summary>
/// IMessageRepository — ConnectHub Message Repositories
/// Methods as specified in the Message-Service Class Diagram (Figure 3)
/// All Task-returning for async/await pattern
/// </summary>
public interface IMessageRepository
{
    // ── Core CRUD ─────────────────────────────────────────────────
    Task<Models.Entities.Message?> FindByMessageId(int messageId);

    Task<IList<Models.Entities.Message>> FindBySenderAndReceiver(int senderId, int receiverId);

    Task<IList<Models.Entities.Message>> FindByRoomId(int roomId);

    Task<IList<Models.Entities.Message>> FindUnreadByReceiverId(int receiverId);

    Task<IList<Models.Entities.Message>> FindRecentMessages(int userId);

    Task<int> CountUnreadByReceiverId(int receiverId);

    Task MarkAllReadByRoomId(int roomId);

    Task DeleteByMessageId(int messageId);

    Task<IList<Models.Entities.Message>> SearchMessages(string query, int userId);

    // ── Persistence ───────────────────────────────────────────────
    Task<Models.Entities.Message> AddMessage(Models.Entities.Message message);

    Task<Models.Entities.Message> UpdateMessage(Models.Entities.Message message);

    Task SaveChangesAsync();

    // ── Extended queries (extra beyond spec) ──────────────────────

    /// <summary>
    /// Returns the most recent message per conversation partner —
    /// powers the sidebar chat contact list (GetRecentChats)
    /// </summary>
    Task<IList<Models.Entities.Message>> GetRecentChats(int userId);

    /// <summary>
    /// Paginated direct message history between two users
    /// GET /api/messages/direct/{userId}
    /// </summary>
    Task<(IList<Models.Entities.Message> Messages, int TotalCount)> GetDirectMessagesPaged(
        int senderId, int receiverId, int page, int pageSize);

    /// <summary>
    /// Paginated room messages
    /// GET /api/messages/room/{roomId}
    /// </summary>
    Task<(IList<Models.Entities.Message> Messages, int TotalCount)> GetRoomMessagesPaged(
        int roomId, int page, int pageSize);

    /// <summary>
    /// Count unread messages from a specific sender
    /// </summary>
    Task<int> CountUnreadFromSender(int receiverId, int senderId);

    /// <summary>
    /// Mark single message as read — pushes read receipt to sender
    /// ChatHub.MarkMessageRead
    /// </summary>
    Task<bool> MarkMessageRead(int messageId, int receiverId);

    /// <summary>
    /// Mark all messages between two users as read
    /// </summary>
    Task MarkAllReadBySender(int senderId, int receiverId);

    // ── Reactions ─────────────────────────────────────────────────
    Task<MessageReaction> AddReaction(MessageReaction reaction);
    Task<bool> RemoveReaction(int messageId, int userId, string emoji);
    Task<IList<MessageReaction>> GetReactions(int messageId);

    // ── Pins ──────────────────────────────────────────────────────
    Task<ConversationPin> PinMessage(ConversationPin pin);
    Task<bool> UnpinMessage(int pinId, int userId);
    Task<IList<ConversationPin>> GetPinnedMessages(int? userId, int? roomId);

    /// <summary>Called by RoomDeletedConsumer — soft-delete all messages in a deleted room</summary>
    Task SoftDeleteAllByRoomId(int roomId);

    /// <summary>
    /// Called by UserDeactivatedConsumer (UC1 event) — soft-delete all messages
    /// sent by the deactivated user so they no longer appear in other users' inboxes.
    /// </summary>
    Task SoftDeleteAllByUserId(int userId);
}
