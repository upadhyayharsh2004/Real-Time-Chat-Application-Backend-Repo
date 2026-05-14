using ConnectHub.Presence.Models.DTOs;
using ConnectHub.Presence.Models.Entities;

namespace ConnectHub.Presence.Services.Interfaces;

/// <summary>
/// IPresenceService — ConnectHub Presence Services
/// Methods EXACTLY as per class diagram (Figure 4.4):
///   UserConnected(int,string):void
///   UserDisconnected(int,string):void
///   GetConnectionsByUserId(int):IList<string>
///   GetOnlineUserIds():IList<int>
///   IsUserOnline(int):bool
///   GetConnectionCount():int
///   GetOnlineUsersInfo():IList<UserConnection>
///   ClearUserConnections(int):void
///
/// Registered as AddSingleton — same ConcurrentDictionary instance shared
/// across ALL Hub connections and API controllers.
/// No DB round-trip for hot-path online status queries.
/// </summary>
public interface IPresenceService
{
    // ── Class diagram sync methods (ConcurrentDictionary — no DB round-trip) ──
    void UserConnected(int userId, string connectionId, string? deviceInfo = null);
    void UserDisconnected(int userId, string connectionId);
    IList<string> GetConnectionsByUserId(int userId);
    IList<int> GetOnlineUserIds();
    bool IsUserOnline(int userId);
    int GetConnectionCount();
    IList<UserConnection> GetOnlineUsersInfo();
    void ClearUserConnections(int userId);

    // ── Async helpers (controller bulk queries, consumer LastActiveAt update) ──
    Task<IList<UserPresenceDto>> GetPresenceForUsers(IList<int> userIds);
    Task<IList<UserPresenceDto>> GetOnlineUsersDtos();
    Task UpdateLastActiveAt(int userId);
}
