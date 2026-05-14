using ConnectHub.ChatRoom.Models.DTOs;

namespace ConnectHub.ChatRoom.Services.Interfaces;

/// <summary>
/// IChatRoomService — ConnectHub ChatRoom Services
/// Same pattern as UC1 IUserService + UC2 IMessageService
/// </summary>
public interface IChatRoomService
{
    // ── Room CRUD ──────────────────────────────────────────────────
    Task<ChatRoomDto> CreateRoom(int creatorUserId, CreateRoomRequestDto dto);
    Task<ChatRoomDto?> GetRoomById(int roomId, int requestingUserId);
    Task<ChatRoomDto> UpdateRoom(int roomId, int requestingUserId, UpdateRoomRequestDto dto);
    Task DeleteRoom(int roomId, int requestingUserId);

    // ── Browse ─────────────────────────────────────────────────────
    /// <summary>GET /api/rooms/public — returns IList&lt;ChatRoom&gt; where RoomType=PUBLIC and IsActive=true</summary>
    Task<PagedRoomsDto> GetPublicRooms(int page = 1, int pageSize = 20);
    Task<IList<ChatRoomDto>> SearchRooms(string query);
    Task<IList<ChatRoomDto>> GetMyRooms(int userId);

    // ── Membership ─────────────────────────────────────────────────
    /// <summary>AddMember / LeaveRoom via Hub or REST; ChatHub.JoinRoom calls Groups.AddToGroupAsync</summary>
    Task<RoomMemberDto> JoinRoom(int roomId, int userId);
    Task LeaveRoom(int roomId, int userId);
    Task<IList<RoomMemberDto>> GetMembers(int roomId);
    Task<bool> IsMember(int roomId, int userId);
    Task<string?> GetMemberRole(int roomId, int userId);

    // ── Role management ────────────────────────────────────────────
    Task<RoomMemberDto> ChangeMemberRole(int roomId, int targetUserId, string newRole, int requestingUserId);
    Task RemoveMember(int roomId, int targetUserId, int requestingUserId);

    // ── Invites ────────────────────────────────────────────────────
    Task<RoomInviteDto> InviteUser(int roomId, int invitedUserId, int invitedByUserId);
    Task<RoomMemberDto> RespondToInvite(int inviteId, int userId, bool accept);
    Task<IList<RoomInviteDto>> GetMyPendingInvites(int userId);

    // ── Admin ops ──────────────────────────────────────────────────
    Task<IList<ChatRoomDto>> AdminGetAllRooms(int page = 1, int pageSize = 20);
    Task AdminDeleteRoom(int roomId, int adminUserId);
}
