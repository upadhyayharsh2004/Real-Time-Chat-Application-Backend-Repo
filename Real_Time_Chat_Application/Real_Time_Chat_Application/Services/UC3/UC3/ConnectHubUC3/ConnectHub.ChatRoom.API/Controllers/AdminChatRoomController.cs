using ConnectHub.ChatRoom.Models.DTOs;
using ConnectHub.ChatRoom.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.ChatRoom.Controllers;

/// <summary>
/// AdminChatRoomController — [Authorize(Roles = "Admin")] only
/// Platform-wide room oversight:
///   - View all rooms (including private)
///   - Delete any room (bypasses member role check via AdminDeleteRoom)
///   - View members of any room
/// </summary>
[ApiController]
[Route("api/admin/rooms")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminChatRoomController : ControllerBase
{
    private readonly IChatRoomService _roomService;
    private readonly ILogger<AdminChatRoomController> _logger;

    public AdminChatRoomController(
        IChatRoomService roomService,
        ILogger<AdminChatRoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/rooms
    /// Platform-wide list of ALL rooms including private (Admin-scoped).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<IList<ChatRoomDto>>), 200)]
    public async Task<IActionResult> GetAllRooms(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var rooms = await _roomService.AdminGetAllRooms(page, pageSize);
        return Ok(ApiResponseDto<IList<ChatRoomDto>>.Ok(rooms));
    }

    /// <summary>
    /// DELETE /api/admin/rooms/{id}
    /// Admin can delete ANY room — bypasses member role check.
    /// Publishes RoomDeletedEvent → UC2 soft-deletes all messages in the room.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminDeleteRoom(int id)
    {
        var adminUserId = GetAdminUserId();
        // ✅ Calls AdminDeleteRoom — bypasses member role check.
        // (DeleteRoom would throw 401 since admin is not a room member)
        await _roomService.AdminDeleteRoom(id, adminUserId);
        _logger.LogWarning("Admin {AdminUserId} deleted Room {RoomId}", adminUserId, id);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Room deleted by admin."));
    }

    /// <summary>
    /// GET /api/admin/rooms/{id}/members
    /// Admin can view members of any room.
    /// </summary>
    [HttpGet("{id:int}/members")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<RoomMemberDto>>), 200)]
    public async Task<IActionResult> GetRoomMembers(int id)
    {
        var members = await _roomService.GetMembers(id);
        return Ok(ApiResponseDto<IList<RoomMemberDto>>.Ok(members));
    }

    private int GetAdminUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && int.TryParse(claim, out var id) ? id : 0;
    }
}
