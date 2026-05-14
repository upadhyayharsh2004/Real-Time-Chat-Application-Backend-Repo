// using ConnectHub.ChatRoom.Models.DTOs;
// using ConnectHub.ChatRoom.Models.Entities;
// using ConnectHub.ChatRoom.Models.Events;
// using ConnectHub.ChatRoom.Repositories.Interfaces;
// using ConnectHub.ChatRoom.Services.Interfaces;

// namespace ConnectHub.ChatRoom.Services.Implementations;

// /// <summary>
// /// ChatRoomService — implements IChatRoomService.
// ///
// /// CORRECT FLOW (same pattern as UC2 MessageService):
// ///   1. Save to SQL Server via EF Core → get valid RoomId
// ///   2. Publish typed event to RabbitMQ AFTER save (RoomId is now valid)
// ///   3. RoomHub then broadcasts via SignalR (Hub calls service, gets back DTO)
// ///
// /// Role hierarchy:  ADMIN > MODERATOR > MEMBER
// ///   ADMIN   — can update/delete room, change roles, remove members, invite
// ///   MODERATOR — can remove members, invite
// ///   MEMBER  — can send messages, view members, leave
// /// </summary>
// public class ChatRoomService : IChatRoomService
// {
//     private readonly IChatRoomRepository _repo;
//     private readonly IRabbitMqPublisher _publisher;
//     private readonly ILogger<ChatRoomService> _logger;

//     public ChatRoomService(
//         IChatRoomRepository repo,
//         IRabbitMqPublisher publisher,
//         ILogger<ChatRoomService> logger)
//     {
//         _repo = repo;
//         _publisher = publisher;
//         _logger = logger;
//     }

//     // ── Room CRUD ──────────────────────────────────────────────────

//     public async Task<ChatRoomDto> CreateRoom(int creatorUserId, CreateRoomRequestDto dto)
//     {
//         if (string.IsNullOrWhiteSpace(dto.Name))
//             throw new ArgumentException("Room name is required.");

//         if (dto.Name.Length < 2)
//             throw new ArgumentException("Room name must be at least 2 characters.");

//         var roomType = dto.RoomType?.ToUpper() ?? "PUBLIC";
//         if (roomType != "PUBLIC" && roomType != "PRIVATE")
//             throw new ArgumentException("RoomType must be PUBLIC or PRIVATE.");

//         var room = new Models.Entities.ChatRoom
//         {
//             Name = dto.Name.Trim(),
//             Description = dto.Description?.Trim(),
//             RoomType = roomType,
//             CreatedByUserId = creatorUserId,
//             AvatarUrl = dto.AvatarUrl,
//             CreatedAt = DateTime.UtcNow,
//             IsActive = true
//         };

//         // ✅ STEP 1: Save to DB → get valid RoomId
//         var saved = await _repo.AddRoom(room);

//         // Creator is automatically added as ADMIN (as per spec)
//         var adminMember = new RoomMember
//         {
//             RoomId = saved.RoomId,
//             UserId = creatorUserId,
//             Role = "ADMIN",
//             JoinedAt = DateTime.UtcNow,
//             IsActive = true
//         };
//         await _repo.AddMember(adminMember);

//         _logger.LogInformation("Room {RoomId} created by {UserId}", saved.RoomId, creatorUserId);

//         // ✅ STEP 2: Publish to RabbitMQ AFTER DB save
//         await _publisher.PublishRoomCreatedAsync(new RoomCreatedEvent
//         {
//             RoomId = saved.RoomId,
//             Name = saved.Name,
//             RoomType = saved.RoomType,
//             CreatedByUserId = creatorUserId,
//             CreatedAt = saved.CreatedAt
//         });

//         return await MapToDto(saved, creatorUserId);
//     }

//     public async Task<ChatRoomDto?> GetRoomById(int roomId, int requestingUserId)
//     {
//         var room = await _repo.FindByRoomId(roomId);
//         if (room is null) return null;

//         // Private rooms: only members can view
//         if (room.RoomType == "PRIVATE" && !await _repo.IsMember(roomId, requestingUserId))
//             throw new UnauthorizedAccessException("You are not a member of this private room.");

//         return await MapToDto(room, requestingUserId);
//     }

//     public async Task<ChatRoomDto> UpdateRoom(int roomId, int requestingUserId, UpdateRoomRequestDto dto)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         var role = await _repo.GetMemberRole(roomId, requestingUserId);
//         if (role != "ADMIN")
//             throw new UnauthorizedAccessException("Only room admins can update room details.");

//         if (!string.IsNullOrWhiteSpace(dto.Name))
//             room.Name = dto.Name.Trim();

//         if (dto.Description is not null)
//             room.Description = dto.Description.Trim();

//         if (!string.IsNullOrWhiteSpace(dto.RoomType))
//         {
//             var rt = dto.RoomType.ToUpper();
//             if (rt != "PUBLIC" && rt != "PRIVATE")
//                 throw new ArgumentException("RoomType must be PUBLIC or PRIVATE.");
//             room.RoomType = rt;
//         }

//         if (dto.AvatarUrl is not null)
//             room.AvatarUrl = dto.AvatarUrl;

//         var updated = await _repo.UpdateRoom(room);
//         _logger.LogInformation("Room {RoomId} updated by {UserId}", roomId, requestingUserId);

//         await _publisher.PublishRoomUpdatedAsync(new RoomUpdatedEvent
//         {
//             RoomId = roomId,
//             Name = updated.Name,
//             Description = updated.Description,
//             UpdatedAt = DateTime.UtcNow
//         });

//         return await MapToDto(updated, requestingUserId);
//     }

//     public async Task DeleteRoom(int roomId, int requestingUserId)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         var role = await _repo.GetMemberRole(roomId, requestingUserId);
//         if (role != "ADMIN")
//             throw new UnauthorizedAccessException("Only room admins can delete the room.");

//         await _repo.DeleteRoom(roomId);
//         _logger.LogInformation("Room {RoomId} deleted by {UserId}", roomId, requestingUserId);

//         // Publishing RoomDeletedEvent triggers UC2 Message-Service to soft-delete all room messages
//         await _publisher.PublishRoomDeletedAsync(new RoomDeletedEvent
//         {
//             RoomId = roomId,
//             DeletedByUserId = requestingUserId,
//             DeletedAt = DateTime.UtcNow
//         });
//     }

//     // ── Browse ─────────────────────────────────────────────────────

//     public async Task<PagedRoomsDto> GetPublicRooms(int page = 1, int pageSize = 20)
//     {
//         var rooms = await _repo.FindPublicRooms(page, pageSize);
//         var total = await _repo.CountPublicRooms();

//         return new PagedRoomsDto
//         {
//             Rooms = rooms.Select(r => MapToDtoSync(r, 0)).ToList(),
//             TotalCount = total,
//             Page = page,
//             PageSize = pageSize,
//             HasMore = (page * pageSize) < total
//         };
//     }

//     public async Task<IList<ChatRoomDto>> SearchRooms(string query)
//     {
//         if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
//             throw new ArgumentException("Search query must be at least 2 characters.");

//         var rooms = await _repo.SearchRooms(query);
//         return rooms.Select(r => MapToDtoSync(r, 0)).ToList();
//     }

//     public async Task<IList<ChatRoomDto>> GetMyRooms(int userId)
//     {
//         var rooms = await _repo.GetUserRooms(userId);
//         var dtos = new List<ChatRoomDto>();

//         foreach (var room in rooms)
//             dtos.Add(await MapToDto(room, userId));

//         return dtos;
//     }

//     // ── Membership ─────────────────────────────────────────────────

//     public async Task<RoomMemberDto> JoinRoom(int roomId, int userId)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         if (room.RoomType == "PRIVATE")
//             throw new InvalidOperationException("This is a private room. You need an invitation to join.");

//         if (await _repo.IsMember(roomId, userId))
//             throw new InvalidOperationException("You are already a member of this room.");

//         var member = new RoomMember
//         {
//             RoomId = roomId,
//             UserId = userId,
//             Role = "MEMBER",
//             JoinedAt = DateTime.UtcNow,
//             IsActive = true
//         };

//         var saved = await _repo.AddMember(member);
//         _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);

//         await _publisher.PublishRoomMemberJoinedAsync(new RoomMemberJoinedEvent
//         {
//             RoomId = roomId,
//             RoomName = room.Name,
//             UserId = userId,
//             UserName = string.Empty, // resolved by Notification-Service via Auth-Service
//             JoinedAt = saved.JoinedAt
//         });

//         return MapMemberToDto(saved);
//     }

//     public async Task LeaveRoom(int roomId, int userId)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         if (!await _repo.IsMember(roomId, userId))
//             throw new InvalidOperationException("You are not a member of this room.");

//         var role = await _repo.GetMemberRole(roomId, userId);

//         // If the only ADMIN leaves, block (must transfer admin first)
//         if (role == "ADMIN")
//         {
//             var members = await _repo.GetRoomMembers(roomId);
//             var otherAdmins = members.Any(m => m.UserId != userId && m.Role == "ADMIN");
//             if (!otherAdmins)
//                 throw new InvalidOperationException(
//                     "You are the only admin. Please assign another admin before leaving.");
//         }

//         await _repo.RemoveMember(roomId, userId);
//         _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);

//         await _publisher.PublishRoomMemberLeftAsync(new RoomMemberLeftEvent
//         {
//             RoomId = roomId,
//             UserId = userId,
//             LeftAt = DateTime.UtcNow
//         });
//     }

//     public async Task<IList<RoomMemberDto>> GetMembers(int roomId)
//     {
//         var members = await _repo.GetRoomMembers(roomId);
//         return members.Select(MapMemberToDto).ToList();
//     }

//     public async Task<bool> IsMember(int roomId, int userId) =>
//         await _repo.IsMember(roomId, userId);

//     public async Task<string?> GetMemberRole(int roomId, int userId) =>
//         await _repo.GetMemberRole(roomId, userId);

//     // ── Role management ────────────────────────────────────────────

//     public async Task<RoomMemberDto> ChangeMemberRole(
//         int roomId, int targetUserId, string newRole, int requestingUserId)
//     {
//         var requestingRole = await _repo.GetMemberRole(roomId, requestingUserId);
//         if (requestingRole != "ADMIN")
//             throw new UnauthorizedAccessException("Only room admins can change member roles.");

//         if (requestingUserId == targetUserId)
//             throw new InvalidOperationException("Admins cannot change their own role.");

//         newRole = newRole.ToUpper();
//         if (newRole != "ADMIN" && newRole != "MODERATOR" && newRole != "MEMBER")
//             throw new ArgumentException("Role must be ADMIN, MODERATOR, or MEMBER.");

//         var member = await _repo.GetMember(roomId, targetUserId)
//             ?? throw new KeyNotFoundException("Member not found in this room.");

//         member.Role = newRole;
//         var updated = await _repo.UpdateMember(member);

//         _logger.LogInformation("User {TargetId} role changed to {Role} in room {RoomId} by {RequestingId}",
//             targetUserId, newRole, roomId, requestingUserId);

//         return MapMemberToDto(updated);
//     }

//     public async Task RemoveMember(int roomId, int targetUserId, int requestingUserId)
//     {
//         var requestingRole = await _repo.GetMemberRole(roomId, requestingUserId);

//         if (requestingRole != "ADMIN" && requestingRole != "MODERATOR")
//             throw new UnauthorizedAccessException("Only admins or moderators can remove members.");

//         var targetRole = await _repo.GetMemberRole(roomId, targetUserId);

//         // Moderators cannot remove admins
//         if (requestingRole == "MODERATOR" && targetRole == "ADMIN")
//             throw new UnauthorizedAccessException("Moderators cannot remove admins.");

//         await _repo.RemoveMember(roomId, targetUserId);
//         _logger.LogInformation("User {TargetId} removed from room {RoomId} by {RequestingId}",
//             targetUserId, roomId, requestingUserId);

//         await _publisher.PublishRoomMemberLeftAsync(new RoomMemberLeftEvent
//         {
//             RoomId = roomId,
//             UserId = targetUserId,
//             LeftAt = DateTime.UtcNow
//         });
//     }

//     // ── Invites ────────────────────────────────────────────────────

//     public async Task<RoomInviteDto> InviteUser(int roomId, int invitedUserId, int invitedByUserId)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         var inviterRole = await _repo.GetMemberRole(roomId, invitedByUserId);
//         if (inviterRole != "ADMIN" && inviterRole != "MODERATOR")
//             throw new UnauthorizedAccessException("Only admins or moderators can invite users.");

//         if (await _repo.IsMember(roomId, invitedUserId))
//             throw new InvalidOperationException("User is already a member of this room.");

//         if (await _repo.HasPendingInvite(roomId, invitedUserId))
//             throw new InvalidOperationException("User already has a pending invite to this room.");

//         var invite = new RoomInvite
//         {
//             RoomId = roomId,
//             InvitedByUserId = invitedByUserId,
//             InvitedUserId = invitedUserId,
//             Status = "PENDING",
//             CreatedAt = DateTime.UtcNow,
//             ExpiresAt = DateTime.UtcNow.AddHours(48)
//         };

//         var saved = await _repo.AddInvite(invite);
//         _logger.LogInformation("User {InvitedId} invited to room {RoomId} by {InviterId}",
//             invitedUserId, roomId, invitedByUserId);

//         return new RoomInviteDto
//         {
//             InviteId = saved.InviteId,
//             RoomId = saved.RoomId,
//             RoomName = room.Name,
//             InvitedByUserId = saved.InvitedByUserId,
//             InvitedUserId = saved.InvitedUserId,
//             Status = saved.Status,
//             CreatedAt = saved.CreatedAt,
//             ExpiresAt = saved.ExpiresAt
//         };
//     }

//     public async Task<RoomMemberDto> RespondToInvite(int inviteId, int userId, bool accept)
//     {
//         var invite = await _repo.GetInvite(inviteId)
//             ?? throw new KeyNotFoundException($"Invite {inviteId} not found.");

//         if (invite.InvitedUserId != userId)
//             throw new UnauthorizedAccessException("This invite is not for you.");

//         if (invite.Status != "PENDING")
//             throw new InvalidOperationException($"Invite is already {invite.Status}.");

//         if (invite.ExpiresAt < DateTime.UtcNow)
//         {
//             invite.Status = "EXPIRED";
//             await _repo.UpdateInvite(invite);
//             throw new InvalidOperationException("This invite has expired.");
//         }

//         if (!accept)
//         {
//             invite.Status = "DECLINED";
//             invite.RespondedAt = DateTime.UtcNow;
//             await _repo.UpdateInvite(invite);
//             return new RoomMemberDto { UserId = userId };
//         }

//         invite.Status = "ACCEPTED";
//         invite.RespondedAt = DateTime.UtcNow;
//         await _repo.UpdateInvite(invite);

//         var member = new RoomMember
//         {
//             RoomId = invite.RoomId,
//             UserId = userId,
//             Role = "MEMBER",
//             JoinedAt = DateTime.UtcNow,
//             IsActive = true
//         };

//         var saved = await _repo.AddMember(member);

//         await _publisher.PublishRoomMemberJoinedAsync(new RoomMemberJoinedEvent
//         {
//             RoomId = invite.RoomId,
//             RoomName = invite.Room?.Name ?? string.Empty,
//             UserId = userId,
//             UserName = string.Empty,
//             JoinedAt = saved.JoinedAt
//         });

//         return MapMemberToDto(saved);
//     }

//     public async Task<IList<RoomInviteDto>> GetMyPendingInvites(int userId)
//     {
//         var invites = await _repo.GetPendingInvitesForUser(userId);
//         return invites.Select(i => new RoomInviteDto
//         {
//             InviteId = i.InviteId,
//             RoomId = i.RoomId,
//             RoomName = i.Room?.Name ?? string.Empty,
//             InvitedByUserId = i.InvitedByUserId,
//             InvitedUserId = i.InvitedUserId,
//             Status = i.Status,
//             CreatedAt = i.CreatedAt,
//             ExpiresAt = i.ExpiresAt
//         }).ToList();
//     }

//     // ── Admin ──────────────────────────────────────────────────────

//     public async Task<IList<ChatRoomDto>> AdminGetAllRooms(int page = 1, int pageSize = 20)
//     {
//         // Admin sees all rooms including private and soft-deleted
//         var rooms = await _repo.FindAllRooms(page, pageSize);
//         return rooms.Select(r => MapToDtoSync(r, 0)).ToList();
//     }

//     // ── Mappers ────────────────────────────────────────────────────

//     private async Task<ChatRoomDto> MapToDto(Models.Entities.ChatRoom room, int requestingUserId)
//     {
//         var memberCount = await _repo.GetMemberCount(room.RoomId);
//         var isMember = requestingUserId > 0 && await _repo.IsMember(room.RoomId, requestingUserId);
//         var role = isMember ? await _repo.GetMemberRole(room.RoomId, requestingUserId) : null;

//         return new ChatRoomDto
//         {
//             RoomId = room.RoomId,
//             Name = room.Name,
//             Description = room.Description,
//             RoomType = room.RoomType,
//             CreatedByUserId = room.CreatedByUserId,
//             CreatedAt = room.CreatedAt,
//             IsActive = room.IsActive,
//             AvatarUrl = room.AvatarUrl,
//             LastMessageContent = room.LastMessageContent,
//             LastMessageAt = room.LastMessageAt,
//             MemberCount = memberCount,
//             IsCurrentUserMember = isMember,
//             CurrentUserRole = role
//         };
//     }

//     private static ChatRoomDto MapToDtoSync(Models.Entities.ChatRoom room, int requestingUserId) =>
//         new()
//         {
//             RoomId = room.RoomId,
//             Name = room.Name,
//             Description = room.Description,
//             RoomType = room.RoomType,
//             CreatedByUserId = room.CreatedByUserId,
//             CreatedAt = room.CreatedAt,
//             IsActive = room.IsActive,
//             AvatarUrl = room.AvatarUrl,
//             LastMessageContent = room.LastMessageContent,
//             LastMessageAt = room.LastMessageAt,
//             MemberCount = room.Members.Count(m => m.IsActive),
//             IsCurrentUserMember = false,
//             CurrentUserRole = null
//         };

//     private static RoomMemberDto MapMemberToDto(RoomMember m) =>
//         new()
//         {
//             RoomMemberId = m.RoomMemberId,
//             RoomId = m.RoomId,
//             UserId = m.UserId,
//             UserName = string.Empty,
//             DisplayName = string.Empty,
//             AvatarUrl = null,
//             Role = m.Role,
//             JoinedAt = m.JoinedAt,
//             IsOnline = false
//         };

//     // ── Admin delete bypass ─────────────────────────────────────────
//     // Called from AdminChatRoomController — bypasses member role check.
//     // Admin is not a room member so DeleteRoom() would throw UnauthorizedAccessException.
//     public async Task AdminDeleteRoom(int roomId, int adminUserId)
//     {
//         var room = await _repo.FindByRoomId(roomId)
//             ?? throw new KeyNotFoundException($"Room {roomId} not found.");

//         await _repo.DeleteRoom(roomId);
//         _logger.LogWarning("Admin {AdminUserId} force-deleted Room {RoomId}", adminUserId, roomId);

//         // Publish RoomDeletedEvent — UC2 Message-Service soft-deletes all room messages
//         await _publisher.PublishRoomDeletedAsync(new RoomDeletedEvent
//         {
//             RoomId = roomId,
//             DeletedByUserId = adminUserId,
//             DeletedAt = DateTime.UtcNow
//         });
//     }

// }



using ConnectHub.ChatRoom.Models.DTOs;
using ConnectHub.ChatRoom.Models.Entities;
using ConnectHub.ChatRoom.Models.Events;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using ConnectHub.ChatRoom.Services.Interfaces;

namespace ConnectHub.ChatRoom.Services.Implementations;

/// <summary>
/// ChatRoomService — implements IChatRoomService.
///
/// CORRECT FLOW (same pattern as UC2 MessageService):
///   1. Save to SQL Server via EF Core → get valid RoomId
///   2. Publish typed event to RabbitMQ AFTER save (RoomId is now valid)
///   3. RoomHub then broadcasts via SignalR (Hub calls service, gets back DTO)
///
/// Role hierarchy:  ADMIN > MODERATOR > MEMBER
///   ADMIN   — can update/delete room, change roles, remove members, invite
///   MODERATOR — can remove members, invite
///   MEMBER  — can send messages, view members, leave
/// </summary>
public class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _repo;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<ChatRoomService> _logger;

    public ChatRoomService(
        IChatRoomRepository repo,
        IRabbitMqPublisher publisher,
        ILogger<ChatRoomService> logger)
    {
        _repo = repo;
        _publisher = publisher;
        _logger = logger;
    }

    // ── Room CRUD ──────────────────────────────────────────────────

    public async Task<ChatRoomDto> CreateRoom(int creatorUserId, CreateRoomRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Room name is required.");

        if (dto.Name.Length < 2)
            throw new ArgumentException("Room name must be at least 2 characters.");

        var roomType = dto.RoomType?.ToUpper() ?? "PUBLIC";
        if (roomType != "PUBLIC" && roomType != "PRIVATE")
            throw new ArgumentException("RoomType must be PUBLIC or PRIVATE.");

        var room = new Models.Entities.ChatRoom
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            RoomType = roomType,
            CreatedByUserId = creatorUserId,
            AvatarUrl = dto.AvatarUrl,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // ✅ STEP 1: Save to DB → get valid RoomId
        var saved = await _repo.AddRoom(room);

        // Creator is automatically added as ADMIN (as per spec)
        var adminMember = new RoomMember
        {
            RoomId = saved.RoomId,
            UserId = creatorUserId,
            Role = "ADMIN",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _repo.AddMember(adminMember);

        _logger.LogInformation("Room {RoomId} created by {UserId}", saved.RoomId, creatorUserId);

        // ✅ STEP 2: Publish to RabbitMQ AFTER DB save
        await _publisher.PublishRoomCreatedAsync(new RoomCreatedEvent
        {
            RoomId = saved.RoomId,
            Name = saved.Name,
            RoomType = saved.RoomType,
            CreatedByUserId = creatorUserId,
            CreatedAt = saved.CreatedAt
        });

        return await MapToDto(saved, creatorUserId);
    }

    public async Task<ChatRoomDto?> GetRoomById(int roomId, int requestingUserId)
    {
        var room = await _repo.FindByRoomId(roomId);
        if (room is null) return null;

        // Private rooms: only members can view
        if (room.RoomType == "PRIVATE" && !await _repo.IsMember(roomId, requestingUserId))
            throw new UnauthorizedAccessException("You are not a member of this private room.");

        return await MapToDto(room, requestingUserId);
    }

    public async Task<ChatRoomDto> UpdateRoom(int roomId, int requestingUserId, UpdateRoomRequestDto dto)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        var role = await _repo.GetMemberRole(roomId, requestingUserId);
        if (role != "ADMIN")
            throw new UnauthorizedAccessException("Only room admins can update room details.");

        if (!string.IsNullOrWhiteSpace(dto.Name))
            room.Name = dto.Name.Trim();

        if (dto.Description is not null)
            room.Description = dto.Description.Trim();

        if (!string.IsNullOrWhiteSpace(dto.RoomType))
        {
            var rt = dto.RoomType.ToUpper();
            if (rt != "PUBLIC" && rt != "PRIVATE")
                throw new ArgumentException("RoomType must be PUBLIC or PRIVATE.");
            room.RoomType = rt;
        }

        if (dto.AvatarUrl is not null)
            room.AvatarUrl = dto.AvatarUrl;

        var updated = await _repo.UpdateRoom(room);
        _logger.LogInformation("Room {RoomId} updated by {UserId}", roomId, requestingUserId);

        await _publisher.PublishRoomUpdatedAsync(new RoomUpdatedEvent
        {
            RoomId = roomId,
            Name = updated.Name,
            Description = updated.Description,
            UpdatedAt = DateTime.UtcNow
        });

        return await MapToDto(updated, requestingUserId);
    }

    public async Task DeleteRoom(int roomId, int requestingUserId)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        var role = await _repo.GetMemberRole(roomId, requestingUserId);
        if (role != "ADMIN")
            throw new UnauthorizedAccessException("Only room admins can delete the room.");

        await _repo.DeleteRoom(roomId);
        _logger.LogInformation("Room {RoomId} deleted by {UserId}", roomId, requestingUserId);

        // Publishing RoomDeletedEvent triggers UC2 Message-Service to soft-delete all room messages
        await _publisher.PublishRoomDeletedAsync(new RoomDeletedEvent
        {
            RoomId = roomId,
            DeletedByUserId = requestingUserId,
            DeletedAt = DateTime.UtcNow
        });
    }

    // ── Browse ─────────────────────────────────────────────────────

    public async Task<PagedRoomsDto> GetPublicRooms(int page = 1, int pageSize = 20)
    {
        var rooms = await _repo.FindPublicRooms(page, pageSize);
        var total = await _repo.CountPublicRooms();

        return new PagedRoomsDto
        {
            Rooms = rooms.Select(r => MapToDtoSync(r, 0)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < total
        };
    }

    public async Task<IList<ChatRoomDto>> SearchRooms(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            throw new ArgumentException("Search query must be at least 2 characters.");

        var rooms = await _repo.SearchRooms(query);
        return rooms.Select(r => MapToDtoSync(r, 0)).ToList();
    }

    public async Task<IList<ChatRoomDto>> GetMyRooms(int userId)
    {
        var rooms = await _repo.GetUserRooms(userId);
        var dtos = new List<ChatRoomDto>();

        foreach (var room in rooms)
            dtos.Add(await MapToDto(room, userId));

        return dtos;
    }

    // ── Membership ─────────────────────────────────────────────────

    public async Task<RoomMemberDto> JoinRoom(int roomId, int userId)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        if (room.RoomType == "PRIVATE")
            throw new InvalidOperationException("This is a private room. You need an invitation to join.");

        if (await _repo.IsMember(roomId, userId))
            throw new InvalidOperationException("You are already a member of this room.");

        var member = new RoomMember
        {
            RoomId = roomId,
            UserId = userId,
            Role = "MEMBER",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var saved = await _repo.AddMember(member);
        _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);

        await _publisher.PublishRoomMemberJoinedAsync(new RoomMemberJoinedEvent
        {
            RoomId = roomId,
            RoomName = room.Name,
            UserId = userId,
            UserName = string.Empty, // resolved by Notification-Service via Auth-Service
            JoinedAt = saved.JoinedAt
        });

        return MapMemberToDto(saved);
    }

    /// <summary>
    /// Production-level LeaveRoom logic (Discord/Slack style):
    ///
    ///   Case 1 — User is NOT admin         → leave directly
    ///   Case 2 — Admin, other admins exist  → leave directly
    ///   Case 3 — Sole admin, other members exist
    ///              → auto-promote the earliest joined non-admin member to ADMIN, then leave
    ///   Case 4 — Sole admin, NO other members (last person)
    ///              → soft-delete the room entirely (no orphan rooms)
    /// </summary>
    public async Task LeaveRoom(int roomId, int userId)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        if (!await _repo.IsMember(roomId, userId))
            throw new InvalidOperationException("You are not a member of this room.");

        var role = await _repo.GetMemberRole(roomId, userId);

        if (role == "ADMIN")
        {
            var members = await _repo.GetRoomMembers(roomId); // ordered by JoinedAt ASC
            var otherAdmins = members.Any(m => m.UserId != userId && m.Role == "ADMIN");

            if (!otherAdmins)
            {
                var otherMembers = members.Where(m => m.UserId != userId).ToList();

                if (otherMembers.Count == 0)
                {
                    // ── Case 4: Last person in room → delete room ──────────────
                    _logger.LogInformation(
                        "User {UserId} is last member of room {RoomId} — deleting room on leave",
                        userId, roomId);

                    await _repo.RemoveMember(roomId, userId);
                    await _repo.DeleteRoom(roomId);

                    await _publisher.PublishRoomDeletedAsync(new RoomDeletedEvent
                    {
                        RoomId = roomId,
                        DeletedByUserId = userId,
                        DeletedAt = DateTime.UtcNow
                    });

                    return;
                }
                else
                {
                    // ── Case 3: Sole admin but others exist → auto-promote ─────
                    // Pick the member who joined earliest (repo returns ordered by JoinedAt ASC)
                    var nextAdmin = otherMembers.First();
                    nextAdmin.Role = "ADMIN";
                    await _repo.UpdateMember(nextAdmin);

                    _logger.LogInformation(
                        "User {UserId} left room {RoomId} — auto-promoted User {NewAdminId} to ADMIN",
                        userId, roomId, nextAdmin.UserId);
                }
            }
            // Case 2: other admins exist → fall through to normal leave below
        }
        // Case 1: regular member → fall through

        // ── Common leave path ──────────────────────────────────────
        await _repo.RemoveMember(roomId, userId);
        _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);

        await _publisher.PublishRoomMemberLeftAsync(new RoomMemberLeftEvent
        {
            RoomId = roomId,
            UserId = userId,
            LeftAt = DateTime.UtcNow
        });
    }

    public async Task<IList<RoomMemberDto>> GetMembers(int roomId)
    {
        var members = await _repo.GetRoomMembers(roomId);
        return members.Select(MapMemberToDto).ToList();
    }

    public async Task<bool> IsMember(int roomId, int userId) =>
        await _repo.IsMember(roomId, userId);

    public async Task<string?> GetMemberRole(int roomId, int userId) =>
        await _repo.GetMemberRole(roomId, userId);

    // ── Role management ────────────────────────────────────────────

    public async Task<RoomMemberDto> ChangeMemberRole(
        int roomId, int targetUserId, string newRole, int requestingUserId)
    {
        var requestingRole = await _repo.GetMemberRole(roomId, requestingUserId);
        if (requestingRole != "ADMIN")
            throw new UnauthorizedAccessException("Only room admins can change member roles.");

        if (requestingUserId == targetUserId)
            throw new InvalidOperationException("Admins cannot change their own role.");

        newRole = newRole.ToUpper();
        if (newRole != "ADMIN" && newRole != "MODERATOR" && newRole != "MEMBER")
            throw new ArgumentException("Role must be ADMIN, MODERATOR, or MEMBER.");

        var member = await _repo.GetMember(roomId, targetUserId)
            ?? throw new KeyNotFoundException("Member not found in this room.");

        member.Role = newRole;
        var updated = await _repo.UpdateMember(member);

        _logger.LogInformation("User {TargetId} role changed to {Role} in room {RoomId} by {RequestingId}",
            targetUserId, newRole, roomId, requestingUserId);

        return MapMemberToDto(updated);
    }

    public async Task RemoveMember(int roomId, int targetUserId, int requestingUserId)
    {
        var requestingRole = await _repo.GetMemberRole(roomId, requestingUserId);

        if (requestingRole != "ADMIN" && requestingRole != "MODERATOR")
            throw new UnauthorizedAccessException("Only admins or moderators can remove members.");

        var targetRole = await _repo.GetMemberRole(roomId, targetUserId);

        // Moderators cannot remove admins
        if (requestingRole == "MODERATOR" && targetRole == "ADMIN")
            throw new UnauthorizedAccessException("Moderators cannot remove admins.");

        await _repo.RemoveMember(roomId, targetUserId);
        _logger.LogInformation("User {TargetId} removed from room {RoomId} by {RequestingId}",
            targetUserId, roomId, requestingUserId);

        await _publisher.PublishRoomMemberLeftAsync(new RoomMemberLeftEvent
        {
            RoomId = roomId,
            UserId = targetUserId,
            LeftAt = DateTime.UtcNow
        });
    }

    // ── Invites ────────────────────────────────────────────────────

    public async Task<RoomInviteDto> InviteUser(int roomId, int invitedUserId, int invitedByUserId)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        var inviterRole = await _repo.GetMemberRole(roomId, invitedByUserId);
        if (inviterRole != "ADMIN" && inviterRole != "MODERATOR")
            throw new UnauthorizedAccessException("Only admins or moderators can invite users.");

        if (await _repo.IsMember(roomId, invitedUserId))
            throw new InvalidOperationException("User is already a member of this room.");

        if (await _repo.HasPendingInvite(roomId, invitedUserId))
            throw new InvalidOperationException("User already has a pending invite to this room.");

        var invite = new RoomInvite
        {
            RoomId = roomId,
            InvitedByUserId = invitedByUserId,
            InvitedUserId = invitedUserId,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        };

        var saved = await _repo.AddInvite(invite);
        _logger.LogInformation("User {InvitedId} invited to room {RoomId} by {InviterId}",
            invitedUserId, roomId, invitedByUserId);

        // ✅ Notification Service ko event bhejo
        await _publisher.PublishRoomInviteSentAsync(new RoomInviteSentEvent
        {
            InviteId = saved.InviteId,
            RoomId = saved.RoomId,
            RoomName = room.Name,
            InvitedUserId = invitedUserId,
            InvitedByUserId = invitedByUserId,
            CreatedAt = saved.CreatedAt,
            ExpiresAt = saved.ExpiresAt
        });

        return new RoomInviteDto
        {
            InviteId = saved.InviteId,
            RoomId = saved.RoomId,
            RoomName = room.Name,
            InvitedByUserId = saved.InvitedByUserId,
            InvitedUserId = saved.InvitedUserId,
            Status = saved.Status,
            CreatedAt = saved.CreatedAt,
            ExpiresAt = saved.ExpiresAt
        };
    }

    public async Task<RoomMemberDto> RespondToInvite(int inviteId, int userId, bool accept)
    {
        var invite = await _repo.GetInvite(inviteId)
            ?? throw new KeyNotFoundException($"Invite {inviteId} not found.");

        if (invite.InvitedUserId != userId)
            throw new UnauthorizedAccessException("This invite is not for you.");

        if (invite.Status != "PENDING")
            throw new InvalidOperationException($"Invite is already {invite.Status}.");

        if (invite.ExpiresAt < DateTime.UtcNow)
        {
            invite.Status = "EXPIRED";
            await _repo.UpdateInvite(invite);
            throw new InvalidOperationException("This invite has expired.");
        }

        if (!accept)
        {
            invite.Status = "DECLINED";
            invite.RespondedAt = DateTime.UtcNow;
            await _repo.UpdateInvite(invite);
            return new RoomMemberDto { UserId = userId };
        }

        invite.Status = "ACCEPTED";
        invite.RespondedAt = DateTime.UtcNow;
        await _repo.UpdateInvite(invite);

        var member = new RoomMember
        {
            RoomId = invite.RoomId,
            UserId = userId,
            Role = "MEMBER",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        var saved = await _repo.AddMember(member);

        await _publisher.PublishRoomMemberJoinedAsync(new RoomMemberJoinedEvent
        {
            RoomId = invite.RoomId,
            RoomName = invite.Room?.Name ?? string.Empty,
            UserId = userId,
            UserName = string.Empty,
            JoinedAt = saved.JoinedAt
        });

        return MapMemberToDto(saved);
    }

    public async Task<IList<RoomInviteDto>> GetMyPendingInvites(int userId)
    {
        var invites = await _repo.GetPendingInvitesForUser(userId);
        return invites.Select(i => new RoomInviteDto
        {
            InviteId = i.InviteId,
            RoomId = i.RoomId,
            RoomName = i.Room?.Name ?? string.Empty,
            InvitedByUserId = i.InvitedByUserId,
            InvitedUserId = i.InvitedUserId,
            Status = i.Status,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt
        }).ToList();
    }

    // ── Admin ──────────────────────────────────────────────────────

    public async Task<IList<ChatRoomDto>> AdminGetAllRooms(int page = 1, int pageSize = 20)
    {
        // Admin sees all rooms including private and soft-deleted
        var rooms = await _repo.FindAllRooms(page, pageSize);
        return rooms.Select(r => MapToDtoSync(r, 0)).ToList();
    }

    // ── Mappers ────────────────────────────────────────────────────

    private async Task<ChatRoomDto> MapToDto(Models.Entities.ChatRoom room, int requestingUserId)
    {
        var memberCount = await _repo.GetMemberCount(room.RoomId);
        var isMember = requestingUserId > 0 && await _repo.IsMember(room.RoomId, requestingUserId);
        var role = isMember ? await _repo.GetMemberRole(room.RoomId, requestingUserId) : null;

        return new ChatRoomDto
        {
            RoomId = room.RoomId,
            Name = room.Name,
            Description = room.Description,
            RoomType = room.RoomType,
            CreatedByUserId = room.CreatedByUserId,
            CreatedAt = room.CreatedAt,
            IsActive = room.IsActive,
            AvatarUrl = room.AvatarUrl,
            LastMessageContent = room.LastMessageContent,
            LastMessageAt = room.LastMessageAt,
            MemberCount = memberCount,
            IsCurrentUserMember = isMember,
            CurrentUserRole = role
        };
    }

    private static ChatRoomDto MapToDtoSync(Models.Entities.ChatRoom room, int requestingUserId) =>
        new()
        {
            RoomId = room.RoomId,
            Name = room.Name,
            Description = room.Description,
            RoomType = room.RoomType,
            CreatedByUserId = room.CreatedByUserId,
            CreatedAt = room.CreatedAt,
            IsActive = room.IsActive,
            AvatarUrl = room.AvatarUrl,
            LastMessageContent = room.LastMessageContent,
            LastMessageAt = room.LastMessageAt,
            MemberCount = room.Members.Count(m => m.IsActive),
            IsCurrentUserMember = false,
            CurrentUserRole = null
        };

    private static RoomMemberDto MapMemberToDto(RoomMember m) =>
        new()
        {
            RoomMemberId = m.RoomMemberId,
            RoomId = m.RoomId,
            UserId = m.UserId,
            UserName = string.Empty,
            DisplayName = string.Empty,
            AvatarUrl = null,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
            IsOnline = false
        };

    // ── Admin delete bypass ─────────────────────────────────────────
    // Called from AdminChatRoomController — bypasses member role check.
    // Admin is not a room member so DeleteRoom() would throw UnauthorizedAccessException.
    public async Task AdminDeleteRoom(int roomId, int adminUserId)
    {
        var room = await _repo.FindByRoomId(roomId)
            ?? throw new KeyNotFoundException($"Room {roomId} not found.");

        await _repo.DeleteRoom(roomId);
        _logger.LogWarning("Admin {AdminUserId} force-deleted Room {RoomId}", adminUserId, roomId);

        // Publish RoomDeletedEvent — UC2 Message-Service soft-deletes all room messages
        await _publisher.PublishRoomDeletedAsync(new RoomDeletedEvent
        {
            RoomId = roomId,
            DeletedByUserId = adminUserId,
            DeletedAt = DateTime.UtcNow
        });
    }
}
