using System.Security.Claims;
using ConnectHub.ChatRoom.Models.DTOs;
using ConnectHub.ChatRoom.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.ChatRoom.Controllers;

/// <summary>
/// ChatRoomController — [ApiController][Route("api/rooms")]
/// REST endpoints for room CRUD, membership, and invite management.
/// Real-time join/leave events are also handled via RoomHub (SignalR).
/// </summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
[Produces("application/json")]
public class ChatRoomController : ControllerBase
{
    private readonly IChatRoomService _roomService;
    private readonly ILogger<ChatRoomController> _logger;

    public ChatRoomController(IChatRoomService roomService, ILogger<ChatRoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    // ── Create room ───────────────────────────────────────────────

    /// <summary>
    /// POST /api/rooms
    /// Creates ChatRoom entity; creator added as RoomMember with Role = ADMIN
    /// Publishes RoomCreatedEvent to RabbitMQ
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<ChatRoomDto>), 201)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 400)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var callerId = GetCallerId();
        var room = await _roomService.CreateRoom(callerId, dto);
        return StatusCode(201, ApiResponseDto<ChatRoomDto>.Ok(room, "Room created successfully."));
    }

    // ── Get room ──────────────────────────────────────────────────

    /// <summary>
    /// GET /api/rooms/{id}
    /// Get room details. Private rooms require membership.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ChatRoomDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRoom(int id)
    {
        var callerId = GetCallerId();
        var room = await _roomService.GetRoomById(id, callerId);

        if (room is null)
            return NotFound(ApiResponseDto<object>.Fail($"Room {id} not found."));

        return Ok(ApiResponseDto<ChatRoomDto>.Ok(room));
    }

    // ── Update room ───────────────────────────────────────────────

    /// <summary>
    /// PUT /api/rooms/{id}
    /// Room admin updates name, description, type.
    /// Publishes RoomUpdatedEvent to RabbitMQ.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<ChatRoomDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRoom(int id, [FromBody] UpdateRoomRequestDto dto)
    {
        var callerId = GetCallerId();
        var room = await _roomService.UpdateRoom(id, callerId, dto);
        return Ok(ApiResponseDto<ChatRoomDto>.Ok(room, "Room updated successfully."));
    }

    // ── Delete room ───────────────────────────────────────────────

    /// <summary>
    /// DELETE /api/rooms/{id}
    /// Room admin soft-deletes the room.
    /// Publishes RoomDeletedEvent → UC2 soft-deletes all room messages.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var callerId = GetCallerId();
        await _roomService.DeleteRoom(id, callerId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Room deleted successfully."));
    }

    // ── Browse rooms ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/rooms/public
    /// Returns IList&lt;ChatRoom&gt; where RoomType=PUBLIC and IsActive=true
    /// </summary>
    [HttpGet("public")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedRoomsDto>), 200)]
    public async Task<IActionResult> GetPublicRooms(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _roomService.GetPublicRooms(page, pageSize);
        return Ok(ApiResponseDto<PagedRoomsDto>.Ok(result));
    }

    /// <summary>
    /// GET /api/rooms/search?q=query
    /// Search public rooms by name or description
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<ChatRoomDto>>), 200)]
    public async Task<IActionResult> SearchRooms([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(ApiResponseDto<object>.Fail("Search query must be at least 2 characters."));

        var rooms = await _roomService.SearchRooms(q);
        return Ok(ApiResponseDto<IList<ChatRoomDto>>.Ok(rooms));
    }

    /// <summary>
    /// GET /api/rooms/my
    /// Get all rooms the authenticated user is a member of
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<ChatRoomDto>>), 200)]
    public async Task<IActionResult> GetMyRooms()
    {
        var callerId = GetCallerId();
        var rooms = await _roomService.GetMyRooms(callerId);
        return Ok(ApiResponseDto<IList<ChatRoomDto>>.Ok(rooms));
    }

    // ── Membership ────────────────────────────────────────────────

    /// <summary>
    /// POST /api/rooms/{id}/join
    /// Join a PUBLIC room. REST endpoint — real-time also available via RoomHub.JoinRoom.
    /// Publishes RoomMemberJoinedEvent to RabbitMQ.
    /// </summary>
    [HttpPost("{id:int}/join")]
    [ProducesResponseType(typeof(ApiResponseDto<RoomMemberDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> JoinRoom(int id)
    {
        var callerId = GetCallerId();
        var member = await _roomService.JoinRoom(id, callerId);
        return Ok(ApiResponseDto<RoomMemberDto>.Ok(member, "Joined room successfully."));
    }

    /// <summary>
    /// POST /api/rooms/{id}/leave
    /// Leave a room. Publishes RoomMemberLeftEvent to RabbitMQ.
    /// </summary>
    [HttpPost("{id:int}/leave")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> LeaveRoom(int id)
    {
        var callerId = GetCallerId();
        await _roomService.LeaveRoom(id, callerId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Left room successfully."));
    }

    /// <summary>
    /// GET /api/rooms/{id}/members
    /// Returns IList&lt;RoomMember&gt; with roles and join dates
    /// </summary>
    [HttpGet("{id:int}/members")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<RoomMemberDto>>), 200)]
    public async Task<IActionResult> GetMembers(int id)
    {
        var members = await _roomService.GetMembers(id);
        return Ok(ApiResponseDto<IList<RoomMemberDto>>.Ok(members));
    }

    // ── Role management ────────────────────────────────────────────

    /// <summary>
    /// PUT /api/rooms/{id}/members/{userId}/role
    /// Change a member's role. Room admin only.
    /// </summary>
    [HttpPut("{id:int}/members/{userId:int}/role")]
    [ProducesResponseType(typeof(ApiResponseDto<RoomMemberDto>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ChangeMemberRole(
        int id, int userId, [FromBody] string role)
    {
        var callerId = GetCallerId();
        var member = await _roomService.ChangeMemberRole(id, userId, role, callerId);
        return Ok(ApiResponseDto<RoomMemberDto>.Ok(member, $"Role changed to {role}."));
    }

    /// <summary>
    /// DELETE /api/rooms/{id}/members/{userId}
    /// Remove a member. Admin or Moderator only.
    /// </summary>
    [HttpDelete("{id:int}/members/{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        var callerId = GetCallerId();
        await _roomService.RemoveMember(id, userId, callerId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Member removed successfully."));
    }

    // ── Invites ───────────────────────────────────────────────────

    /// <summary>
    /// POST /api/rooms/{id}/invite
    /// Invite a user to a PRIVATE room. Admin or Moderator only.
    /// </summary>
    [HttpPost("{id:int}/invite")]
    [ProducesResponseType(typeof(ApiResponseDto<RoomInviteDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> InviteUser(int id, [FromBody] InviteUserRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed"));

        var callerId = GetCallerId();
        var invite = await _roomService.InviteUser(id, dto.InvitedUserId, callerId);
        return StatusCode(201, ApiResponseDto<RoomInviteDto>.Ok(invite, "Invite sent."));
    }

    /// <summary>
    /// PUT /api/rooms/invites/{inviteId}/respond
    /// Accept or decline an invite.
    /// </summary>
    [HttpPut("invites/{inviteId:int}/respond")]
    [ProducesResponseType(typeof(ApiResponseDto<RoomMemberDto>), 200)]
    public async Task<IActionResult> RespondToInvite(
        int inviteId, [FromBody] RespondToInviteRequestDto dto)
    {
        var callerId = GetCallerId();
        var member = await _roomService.RespondToInvite(inviteId, callerId, dto.Accept);
        var msg = dto.Accept ? "Invite accepted. You have joined the room." : "Invite declined.";
        return Ok(ApiResponseDto<RoomMemberDto>.Ok(member, msg));
    }

    /// <summary>
    /// GET /api/rooms/invites/pending
    /// Get all pending invites for the authenticated user.
    /// </summary>
    [HttpGet("invites/pending")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<RoomInviteDto>>), 200)]
    public async Task<IActionResult> GetMyPendingInvites()
    {
        var callerId = GetCallerId();
        var invites = await _roomService.GetMyPendingInvites(callerId);
        return Ok(ApiResponseDto<IList<RoomInviteDto>>.Ok(invites));
    }

    // ── Helper ────────────────────────────────────────────────────

    private int GetCallerId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (claim is null || !int.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("Unable to determine caller identity.");

        return id;
    }
}
