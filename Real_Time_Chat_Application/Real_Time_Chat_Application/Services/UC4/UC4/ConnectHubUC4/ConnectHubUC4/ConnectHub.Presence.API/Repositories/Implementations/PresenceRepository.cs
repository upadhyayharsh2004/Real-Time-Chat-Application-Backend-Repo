using ConnectHub.Presence.Data;
using ConnectHub.Presence.Models.Entities;
using ConnectHub.Presence.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Presence.Repositories.Implementations;

public class PresenceRepository : IPresenceRepository
{
    private readonly PresenceDbContext _context;

    public PresenceRepository(PresenceDbContext context)
    {
        _context = context;
    }

    public async Task<UserPresence?> GetByUserId(int userId) =>
        await _context.UserPresences.FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<UserPresence> GetOrCreateByUserId(int userId)
    {
        var presence = await _context.UserPresences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (presence is not null) return presence;

        presence = new UserPresence
        {
            UserId = userId,
            IsOnline = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.UserPresences.AddAsync(presence);
        await _context.SaveChangesAsync();
        return presence;
    }

    public async Task SetUserOnline(int userId, int connectionCount)
    {
        var presence = await GetOrCreateByUserId(userId);
        presence.IsOnline = true;
        presence.LastActiveAt = DateTime.UtcNow;
        presence.ActiveConnectionCount = connectionCount;
        presence.UpdatedAt = DateTime.UtcNow;
        _context.UserPresences.Update(presence);
        await _context.SaveChangesAsync();
    }

    public async Task SetUserOffline(int userId, DateTime lastSeen)
    {
        var presence = await GetOrCreateByUserId(userId);
        presence.IsOnline = false;
        presence.LastSeen = lastSeen;
        presence.ActiveConnectionCount = 0;
        presence.UpdatedAt = DateTime.UtcNow;
        _context.UserPresences.Update(presence);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateLastActiveAt(int userId, DateTime lastActiveAt)
    {
        var presence = await GetOrCreateByUserId(userId);
        presence.LastActiveAt = lastActiveAt;
        presence.UpdatedAt = DateTime.UtcNow;
        _context.UserPresences.Update(presence);
        await _context.SaveChangesAsync();
    }

    public async Task<IList<UserPresence>> GetPresenceForUsers(IList<int> userIds) =>
        await _context.UserPresences
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync();

    public async Task AddConnection(UserConnection connection)
    {
        // Upsert: remove existing then re-add (handles reconnection)
        var existing = await _context.UserConnections.FindAsync(connection.ConnectionId);
        if (existing is not null)
            _context.UserConnections.Remove(existing);

        await _context.UserConnections.AddAsync(connection);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveConnection(string connectionId)
    {
        var conn = await _context.UserConnections.FindAsync(connectionId);
        if (conn is null) return;
        _context.UserConnections.Remove(conn);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAllConnectionsForUser(int userId)
    {
        var conns = await _context.UserConnections
            .Where(c => c.UserId == userId).ToListAsync();
        _context.UserConnections.RemoveRange(conns);
        await _context.SaveChangesAsync();
    }

    public async Task<IList<UserConnection>> GetConnectionsForUser(int userId) =>
        await _context.UserConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync();
}
