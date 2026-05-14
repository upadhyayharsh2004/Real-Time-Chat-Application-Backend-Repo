// using ConnectHub.ChatRoom.Data;
// using ConnectHub.ChatRoom.Models.Entities;
// using ConnectHub.ChatRoom.Repositories.Interfaces;
// using Microsoft.EntityFrameworkCore;

// namespace ConnectHub.ChatRoom.Repositories.Implementations;

// /// <summary>
// /// ChatRoomRepository — EF Core implementation of IChatRoomRepository.
// /// Uses composite indexes for common queries (public room browsing, member lookup, role check).
// /// Soft delete: IsActive = false, DeletedAt = DateTime.UtcNow.
// /// </summary>
// public class ChatRoomRepository : IChatRoomRepository
// {
//     private readonly ChatRoomDbContext _context;

//     public ChatRoomRepository(ChatRoomDbContext context)
//     {
//         _context = context;
//     }

//     // ── ChatRoom ──────────────────────────────────────────────────

//     public async Task<Models.Entities.ChatRoom?> FindByRoomId(int roomId) =>
//         await _context.ChatRooms
//             .Include(r => r.Members.Where(m => m.IsActive))
//             .FirstOrDefaultAsync(r => r.RoomId == roomId && r.IsActive);

//     public async Task<IList<Models.Entities.ChatRoom>> FindPublicRooms(int page, int pageSize) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .Where(r => r.RoomType == "PUBLIC" && r.IsActive)
//             .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
//             .Skip((page - 1) * pageSize)
//             .Take(pageSize)
//             .ToListAsync();


//     public async Task<IList<Models.Entities.ChatRoom>> FindAllRooms(int page, int pageSize) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
//             .Skip((page - 1) * pageSize)
//             .Take(pageSize)
//             .ToListAsync();

//     public async Task<int> CountPublicRooms() =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .CountAsync(r => r.RoomType == "PUBLIC" && r.IsActive);

//     public async Task<IList<Models.Entities.ChatRoom>> FindRoomsByUserId(int userId) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .Where(r => r.IsActive &&
//                 r.Members.Any(m => m.UserId == userId && m.IsActive))
//             .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
//             .ToListAsync();

//     public async Task<IList<Models.Entities.ChatRoom>> SearchRooms(string query) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .Where(r => r.IsActive &&
//                 (EF.Functions.Like(r.Name.ToLower(), $"%{query.ToLower()}%") ||
//                  (r.Description != null && EF.Functions.Like(r.Description.ToLower(), $"%{query.ToLower()}%"))))
//             .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
//             .Take(30)
//             .ToListAsync();

//     public async Task<Models.Entities.ChatRoom> AddRoom(Models.Entities.ChatRoom room)
//     {
//         await _context.ChatRooms.AddAsync(room);
//         await _context.SaveChangesAsync();
//         return room;
//     }

//     public async Task<Models.Entities.ChatRoom> UpdateRoom(Models.Entities.ChatRoom room)
//     {
//         _context.ChatRooms.Update(room);
//         await _context.SaveChangesAsync();
//         return room;
//     }

//     public async Task DeleteRoom(int roomId)
//     {
//         var room = await _context.ChatRooms.FindAsync(roomId);
//         if (room is null) return;

//         // Soft delete
//         room.IsActive = false;
//         room.DeletedAt = DateTime.UtcNow;
//         await _context.SaveChangesAsync();
//     }

//     public async Task<bool> RoomNameExists(string name) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .AnyAsync(r => r.Name.ToLower() == name.ToLower() && r.IsActive);

//     public async Task UpdateLastMessage(int roomId, string content, DateTime sentAt)
//     {
//         var room = await _context.ChatRooms.FindAsync(roomId);
//         if (room is null) return;

//         room.LastMessageContent = content.Length > 100
//             ? content[..100] + "..."
//             : content;
//         room.LastMessageAt = sentAt;
//         await _context.SaveChangesAsync();
//     }

//     // ── RoomMember ────────────────────────────────────────────────

//     public async Task<IList<RoomMember>> GetRoomMembers(int roomId) =>
//         await _context.RoomMembers
//             .AsNoTracking()
//             .Where(m => m.RoomId == roomId && m.IsActive)
//             .OrderBy(m => m.JoinedAt)
//             .ToListAsync();

//     public async Task<RoomMember?> GetMember(int roomId, int userId) =>
//         await _context.RoomMembers
//             .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

//     public async Task<RoomMember> AddMember(RoomMember member)
//     {
//         // Check for existing (left) membership and reactivate
//         var existing = await _context.RoomMembers
//             .FirstOrDefaultAsync(m => m.RoomId == member.RoomId && m.UserId == member.UserId);

//         if (existing is not null)
//         {
//             existing.IsActive = true;
//             existing.LeftAt = null;
//             existing.JoinedAt = DateTime.UtcNow;
//             existing.Role = member.Role;
//             await _context.SaveChangesAsync();
//             return existing;
//         }

//         await _context.RoomMembers.AddAsync(member);
//         await _context.SaveChangesAsync();
//         return member;
//     }

//     public async Task<RoomMember> UpdateMember(RoomMember member)
//     {
//         _context.RoomMembers.Update(member);
//         await _context.SaveChangesAsync();
//         return member;
//     }

//     public async Task RemoveMember(int roomId, int userId)
//     {
//         var member = await _context.RoomMembers
//             .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

//         if (member is null) return;

//         member.IsActive = false;
//         member.LeftAt = DateTime.UtcNow;
//         await _context.SaveChangesAsync();
//     }

//     public async Task<bool> IsMember(int roomId, int userId) =>
//         await _context.RoomMembers
//             .AsNoTracking()
//             .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

//     public async Task<string?> GetMemberRole(int roomId, int userId)
//     {
//         var member = await _context.RoomMembers
//             .AsNoTracking()
//             .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

//         return member?.Role;
//     }

//     public async Task<int> GetMemberCount(int roomId) =>
//         await _context.RoomMembers
//             .AsNoTracking()
//             .CountAsync(m => m.RoomId == roomId && m.IsActive);

//     public async Task<IList<Models.Entities.ChatRoom>> GetUserRooms(int userId) =>
//         await _context.ChatRooms
//             .AsNoTracking()
//             .Where(r => r.IsActive &&
//                 r.Members.Any(m => m.UserId == userId && m.IsActive))
//             .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
//             .ToListAsync();

//     public async Task RemoveUserFromAllRooms(int userId)
//     {
//         var memberships = await _context.RoomMembers
//             .Where(m => m.UserId == userId && m.IsActive)
//             .ToListAsync();

//         foreach (var m in memberships)
//         {
//             m.IsActive = false;
//             m.LeftAt = DateTime.UtcNow;
//         }

//         await _context.SaveChangesAsync();
//     }

//     // ── RoomInvite ────────────────────────────────────────────────

//     public async Task<RoomInvite> AddInvite(RoomInvite invite)
//     {
//         await _context.RoomInvites.AddAsync(invite);
//         await _context.SaveChangesAsync();
//         return invite;
//     }

//     public async Task<RoomInvite?> GetInvite(int inviteId) =>
//         await _context.RoomInvites
//             .Include(i => i.Room)
//             .FirstOrDefaultAsync(i => i.InviteId == inviteId);

//     public async Task<IList<RoomInvite>> GetPendingInvitesForUser(int userId) =>
//         await _context.RoomInvites
//             .AsNoTracking()
//             .Include(i => i.Room)
//             .Where(i =>
//                 i.InvitedUserId == userId &&
//                 i.Status == "PENDING" &&
//                 i.ExpiresAt > DateTime.UtcNow)
//             .OrderByDescending(i => i.CreatedAt)
//             .ToListAsync();

//     public async Task<RoomInvite> UpdateInvite(RoomInvite invite)
//     {
//         _context.RoomInvites.Update(invite);
//         await _context.SaveChangesAsync();
//         return invite;
//     }

//     public async Task<bool> HasPendingInvite(int roomId, int userId) =>
//         await _context.RoomInvites
//             .AsNoTracking()
//             .AnyAsync(i =>
//                 i.RoomId == roomId &&
//                 i.InvitedUserId == userId &&
//                 i.Status == "PENDING" &&
//                 i.ExpiresAt > DateTime.UtcNow);

//     public async Task SaveChangesAsync() =>
//         await _context.SaveChangesAsync();
// }



using ConnectHub.ChatRoom.Data;
using ConnectHub.ChatRoom.Models.Entities;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.ChatRoom.Repositories.Implementations;

/// <summary>
/// ChatRoomRepository — EF Core implementation of IChatRoomRepository.
/// Uses composite indexes for common queries (public room browsing, member lookup, role check).
/// Soft delete: IsActive = false, DeletedAt = DateTime.UtcNow.
/// </summary>
public class ChatRoomRepository : IChatRoomRepository
{
    private readonly ChatRoomDbContext _context;

    public ChatRoomRepository(ChatRoomDbContext context)
    {
        _context = context;
    }

    // ── ChatRoom ──────────────────────────────────────────────────

    public async Task<Models.Entities.ChatRoom?> FindByRoomId(int roomId) =>
        await _context.ChatRooms
            .Include(r => r.Members.Where(m => m.IsActive))
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.IsActive);

    public async Task<IList<Models.Entities.ChatRoom>> FindPublicRooms(int page, int pageSize) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.Members.Where(m => m.IsActive))  // ← FIXED: load members so MemberCount works
            .Where(r => r.RoomType == "PUBLIC" && r.IsActive)
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<IList<Models.Entities.ChatRoom>> FindAllRooms(int page, int pageSize) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.Members.Where(m => m.IsActive))  // ← FIXED: load members so MemberCount works
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> CountPublicRooms() =>
        await _context.ChatRooms
            .AsNoTracking()
            .CountAsync(r => r.RoomType == "PUBLIC" && r.IsActive);

    public async Task<IList<Models.Entities.ChatRoom>> FindRoomsByUserId(int userId) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Where(r => r.IsActive &&
                r.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .ToListAsync();

    public async Task<IList<Models.Entities.ChatRoom>> SearchRooms(string query) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Include(r => r.Members.Where(m => m.IsActive))  // ← FIXED: load members so MemberCount works
            .Where(r => r.IsActive &&
                (EF.Functions.Like(r.Name.ToLower(), $"%{query.ToLower()}%") ||
                 (r.Description != null && EF.Functions.Like(r.Description.ToLower(), $"%{query.ToLower()}%"))))
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .Take(30)
            .ToListAsync();

    public async Task<Models.Entities.ChatRoom> AddRoom(Models.Entities.ChatRoom room)
    {
        await _context.ChatRooms.AddAsync(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task<Models.Entities.ChatRoom> UpdateRoom(Models.Entities.ChatRoom room)
    {
        _context.ChatRooms.Update(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task DeleteRoom(int roomId)
    {
        var room = await _context.ChatRooms.FindAsync(roomId);
        if (room is null) return;

        // Soft delete
        room.IsActive = false;
        room.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RoomNameExists(string name) =>
        await _context.ChatRooms
            .AsNoTracking()
            .AnyAsync(r => r.Name.ToLower() == name.ToLower() && r.IsActive);

    public async Task UpdateLastMessage(int roomId, string content, DateTime sentAt)
    {
        var room = await _context.ChatRooms.FindAsync(roomId);
        if (room is null) return;

        room.LastMessageContent = content.Length > 100
            ? content[..100] + "..."
            : content;
        room.LastMessageAt = sentAt;
        await _context.SaveChangesAsync();
    }

    // ── RoomMember ────────────────────────────────────────────────

    public async Task<IList<RoomMember>> GetRoomMembers(int roomId) =>
        await _context.RoomMembers
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && m.IsActive)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();

    public async Task<RoomMember?> GetMember(int roomId, int userId) =>
        await _context.RoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

    public async Task<RoomMember> AddMember(RoomMember member)
    {
        // Check for existing (left) membership and reactivate
        var existing = await _context.RoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == member.RoomId && m.UserId == member.UserId);

        if (existing is not null)
        {
            existing.IsActive = true;
            existing.LeftAt = null;
            existing.JoinedAt = DateTime.UtcNow;
            existing.Role = member.Role;
            await _context.SaveChangesAsync();
            return existing;
        }

        await _context.RoomMembers.AddAsync(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task<RoomMember> UpdateMember(RoomMember member)
    {
        _context.RoomMembers.Update(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task RemoveMember(int roomId, int userId)
    {
        var member = await _context.RoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

        if (member is null) return;

        member.IsActive = false;
        member.LeftAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsMember(int roomId, int userId) =>
        await _context.RoomMembers
            .AsNoTracking()
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

    public async Task<string?> GetMemberRole(int roomId, int userId)
    {
        var member = await _context.RoomMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

        return member?.Role;
    }

    public async Task<int> GetMemberCount(int roomId) =>
        await _context.RoomMembers
            .AsNoTracking()
            .CountAsync(m => m.RoomId == roomId && m.IsActive);

    public async Task<IList<Models.Entities.ChatRoom>> GetUserRooms(int userId) =>
        await _context.ChatRooms
            .AsNoTracking()
            .Where(r => r.IsActive &&
                r.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .ToListAsync();

    public async Task RemoveUserFromAllRooms(int userId)
    {
        var memberships = await _context.RoomMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .ToListAsync();

        foreach (var m in memberships)
        {
            m.IsActive = false;
            m.LeftAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    // ── RoomInvite ────────────────────────────────────────────────

    public async Task<RoomInvite> AddInvite(RoomInvite invite)
    {
        await _context.RoomInvites.AddAsync(invite);
        await _context.SaveChangesAsync();
        return invite;
    }

    public async Task<RoomInvite?> GetInvite(int inviteId) =>
        await _context.RoomInvites
            .Include(i => i.Room)
            .FirstOrDefaultAsync(i => i.InviteId == inviteId);

    public async Task<IList<RoomInvite>> GetPendingInvitesForUser(int userId) =>
        await _context.RoomInvites
            .AsNoTracking()
            .Include(i => i.Room)
            .Where(i =>
                i.InvitedUserId == userId &&
                i.Status == "PENDING" &&
                i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    public async Task<RoomInvite> UpdateInvite(RoomInvite invite)
    {
        _context.RoomInvites.Update(invite);
        await _context.SaveChangesAsync();
        return invite;
    }

    public async Task<bool> HasPendingInvite(int roomId, int userId) =>
        await _context.RoomInvites
            .AsNoTracking()
            .AnyAsync(i =>
                i.RoomId == roomId &&
                i.InvitedUserId == userId &&
                i.Status == "PENDING" &&
                i.ExpiresAt > DateTime.UtcNow);

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}