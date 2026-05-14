using ConnectHub.ChatRoom.Models.Entities;

namespace ConnectHub.ChatRoom.Repositories.Interfaces;

/// <summary>
/// IChatRoomRepository — ConnectHub ChatRoom Repositories
/// All Task-returning async methods. Same pattern as UC1 IUserRepository + UC2 IMessageRepository.
/// </summary>
public interface IChatRoomRepository
{
    // ── ChatRoom CRUD ──────────────────────────────────────────────
    Task<Models.Entities.ChatRoom?> FindByRoomId(int roomId);
    Task<IList<Models.Entities.ChatRoom>> FindPublicRooms(int page, int pageSize);
    Task<IList<Models.Entities.ChatRoom>> FindAllRooms(int page, int pageSize);
    Task<int> CountPublicRooms();
    Task<IList<Models.Entities.ChatRoom>> FindRoomsByUserId(int userId);
    Task<IList<Models.Entities.ChatRoom>> SearchRooms(string query);
    Task<Models.Entities.ChatRoom> AddRoom(Models.Entities.ChatRoom room);
    Task<Models.Entities.ChatRoom> UpdateRoom(Models.Entities.ChatRoom room);
    Task DeleteRoom(int roomId);
    Task<bool> RoomNameExists(string name);

    /// <summary>Update last message preview — called by RoomMessageSentEvent consumer</summary>
    Task UpdateLastMessage(int roomId, string content, DateTime sentAt);

    // ── RoomMember CRUD ────────────────────────────────────────────
    Task<IList<RoomMember>> GetRoomMembers(int roomId);
    Task<RoomMember?> GetMember(int roomId, int userId);
    Task<RoomMember> AddMember(RoomMember member);
    Task<RoomMember> UpdateMember(RoomMember member);
    Task RemoveMember(int roomId, int userId);
    Task<bool> IsMember(int roomId, int userId);
    Task<string?> GetMemberRole(int roomId, int userId);
    Task<int> GetMemberCount(int roomId);
    Task<IList<Models.Entities.ChatRoom>> GetUserRooms(int userId);

    /// <summary>Remove user from all rooms — called by UserDeactivatedEvent consumer</summary>
    Task RemoveUserFromAllRooms(int userId);

    // ── RoomInvite CRUD ────────────────────────────────────────────
    Task<RoomInvite> AddInvite(RoomInvite invite);
    Task<RoomInvite?> GetInvite(int inviteId);
    Task<IList<RoomInvite>> GetPendingInvitesForUser(int userId);
    Task<RoomInvite> UpdateInvite(RoomInvite invite);
    Task<bool> HasPendingInvite(int roomId, int userId);
    Task SaveChangesAsync();
}
