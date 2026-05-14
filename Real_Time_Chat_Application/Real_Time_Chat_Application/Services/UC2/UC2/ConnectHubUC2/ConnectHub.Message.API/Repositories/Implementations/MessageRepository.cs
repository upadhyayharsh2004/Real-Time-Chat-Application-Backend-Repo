using ConnectHub.Message.Data;
using ConnectHub.Message.Models.Entities;
using ConnectHub.Message.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Message.Repositories.Implementations;

/// <summary>
/// MessageRepository — EF Core implementation of IMessageRepository
/// Uses (SenderId, ReceiverId) and (RoomId, SentAt) composite indexes for performance
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly MessageDbContext _context;

    public MessageRepository(MessageDbContext context)
    {
        _context = context;
    }

    // ── Core CRUD ─────────────────────────────────────────────────

    public async Task<Models.Entities.Message?> FindByMessageId(int messageId) =>
        await _context.Messages
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .FirstOrDefaultAsync(m => m.MessageId == messageId);

    public async Task<IList<Models.Entities.Message>> FindBySenderAndReceiver(
        int senderId, int receiverId) =>
        await _context.Messages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .Where(m =>
                (m.SenderId == senderId && m.ReceiverId == receiverId) ||
                (m.SenderId == receiverId && m.ReceiverId == senderId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

    public async Task<IList<Models.Entities.Message>> FindByRoomId(int roomId) =>
        await _context.Messages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .Where(m => m.RoomId == roomId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

    public async Task<IList<Models.Entities.Message>> FindUnreadByReceiverId(int receiverId) =>
        await _context.Messages
            .AsNoTracking()
            .Where(m => m.ReceiverId == receiverId && !m.IsRead && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

    public async Task<IList<Models.Entities.Message>> FindRecentMessages(int userId) =>
        await _context.Messages
            .AsNoTracking()
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .ToListAsync();

    public async Task<int> CountUnreadByReceiverId(int receiverId) =>
        await _context.Messages
            .AsNoTracking()
            .CountAsync(m => m.ReceiverId == receiverId && !m.IsRead && !m.IsDeleted);

    public async Task MarkAllReadByRoomId(int roomId)
    {
        var messages = await _context.Messages
            .Where(m => m.RoomId == roomId && !m.IsRead)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteByMessageId(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message is null) return;

        // Soft delete — IsDeleted = true; content replaced with placeholder
        message.IsDeleted = true;
        message.Content = "[Message deleted]";

        await _context.SaveChangesAsync();
    }

    public async Task<IList<Models.Entities.Message>> SearchMessages(string query, int userId) =>
        await _context.Messages
            .AsNoTracking()
            .Where(m =>
                !m.IsDeleted &&
                (m.SenderId == userId || m.ReceiverId == userId) &&
                EF.Functions.Like(m.Content.ToLower(), $"%{query.ToLower()}%"))
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .ToListAsync();

    public async Task<Models.Entities.Message> AddMessage(Models.Entities.Message message)
    {
        await _context.Messages.AddAsync(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<Models.Entities.Message> UpdateMessage(Models.Entities.Message message)
    {
        _context.Messages.Update(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();

    // ── Extended queries ──────────────────────────────────────────

    public async Task<IList<Models.Entities.Message>> GetRecentChats(int userId)
    {
        // Sabhi messages lo taaki deleted ke baad previous mil sake
        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m =>
                (m.SenderId == userId || m.ReceiverId == userId)
                && m.ReceiverId != null
                && m.RoomId == null)
            .OrderByDescending(m => m.SentAt)
            .Take(200)
            .ToListAsync();

        return messages;
    }

    public async Task<(IList<Models.Entities.Message> Messages, int TotalCount)> GetDirectMessagesPaged(
        int senderId, int receiverId, int page, int pageSize)
    {
        var query = _context.Messages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .Where(m =>
                (m.SenderId == senderId && m.ReceiverId == receiverId) ||
                (m.SenderId == receiverId && m.ReceiverId == senderId))
            .OrderByDescending(m => m.SentAt);

        var total = await query.CountAsync();

        var messages = await query.Include(m => m.Reactions).Include(m => m.ReplyToMessage).AsSplitQuery()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Return in chronological order for the chat window
        return (messages.OrderBy(m => m.SentAt).ToList(), total);
    }

    public async Task<(IList<Models.Entities.Message> Messages, int TotalCount)> GetRoomMessagesPaged(
        int roomId, int page, int pageSize)
    {
        var query = _context.Messages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Include(m => m.ReplyToMessage)
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.SentAt);

        var total = await query.CountAsync();

        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (messages.OrderBy(m => m.SentAt).ToList(), total);
    }

    public async Task<int> CountUnreadFromSender(int receiverId, int senderId) =>
        await _context.Messages
            .AsNoTracking()
            .CountAsync(m =>
                m.ReceiverId == receiverId &&
                m.SenderId == senderId &&
                !m.IsRead &&
                !m.IsDeleted);

    public async Task<bool> MarkMessageRead(int messageId, int receiverId)
    {
        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId && m.ReceiverId == receiverId);

        if (message is null) return false;

        message.IsRead = true;
        message.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllReadBySender(int senderId, int receiverId)
    {
        var messages = await _context.Messages
            .Where(m =>
                m.SenderId == senderId &&
                m.ReceiverId == receiverId &&
                !m.IsRead)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    // ── Reactions ─────────────────────────────────────────────────

    public async Task<MessageReaction> AddReaction(MessageReaction reaction)
    {
        await _context.MessageReactions.AddAsync(reaction);
        await _context.SaveChangesAsync();
        return reaction;
    }

    public async Task<bool> RemoveReaction(int messageId, int userId, string emoji)
    {
        var reaction = await _context.MessageReactions
            .FirstOrDefaultAsync(r =>
                r.MessageId == messageId &&
                r.UserId == userId &&
                r.Emoji == emoji);

        if (reaction is null) return false;

        _context.MessageReactions.Remove(reaction);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IList<MessageReaction>> GetReactions(int messageId) =>
        await _context.MessageReactions
            .AsNoTracking()
            .Where(r => r.MessageId == messageId)
            .OrderBy(r => r.ReactedAt)
            .ToListAsync();

    // ── Pins ──────────────────────────────────────────────────────

    public async Task<ConversationPin> PinMessage(ConversationPin pin)
    {
        await _context.ConversationPins.AddAsync(pin);
        await _context.SaveChangesAsync();
        return pin;
    }

    public async Task<bool> UnpinMessage(int pinId, int userId)
    {
        var pin = await _context.ConversationPins
            .FirstOrDefaultAsync(p => p.PinId == pinId && p.PinnedByUserId == userId);

        if (pin is null) return false;

        _context.ConversationPins.Remove(pin);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IList<ConversationPin>> GetPinnedMessages(int? userId, int? roomId)
    {
        var query = _context.ConversationPins
            .AsNoTracking()
            .Include(p => p.Message)
                .ThenInclude(m => m.Reactions)
            .AsQueryable();

        if (roomId.HasValue)
            query = query.Where(p => p.RoomId == roomId);
        else if (userId.HasValue)
            query = query.Where(p =>
                p.ConversationWithUserId == userId || p.PinnedByUserId == userId);

        return await query.OrderByDescending(p => p.PinnedAt).ToListAsync();
    }
    public async Task SoftDeleteAllByRoomId(int roomId)
    {
        var messages = await _context.Messages
            .Where(m => m.RoomId == roomId && !m.IsDeleted)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsDeleted = true;
            msg.Content = "[Message deleted]";
        }
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAllByUserId(int userId)
    {
        var messages = await _context.Messages
            .Where(m => m.SenderId == userId && !m.IsDeleted)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsDeleted = true;
            msg.Content = "[Message deleted]";
        }
        await _context.SaveChangesAsync();
    }

}
