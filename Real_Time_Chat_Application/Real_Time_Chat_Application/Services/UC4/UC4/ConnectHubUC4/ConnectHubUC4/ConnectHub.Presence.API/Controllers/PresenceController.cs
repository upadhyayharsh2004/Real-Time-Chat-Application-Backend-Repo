using ConnectHub.Presence.Models.DTOs;
using ConnectHub.Presence.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Presence.Controllers;

/// <summary>
/// PresenceController — [ApiController][Route("api/presence")]
/// As per class diagram:
///   GetOnlineUsers()     → IActionResult
///   IsUserOnline(userId) → IActionResult
///   GetConnectionCount() → IActionResult
///   GetOnlineUsersInfo() → IActionResult
///
/// All hot-path queries read from ConcurrentDictionary (no DB round trip).
/// IPresenceService is AddSingleton — same instance as PresenceHub.
/// </summary>
[ApiController]
[Route("api/presence")]
[Authorize]
[Produces("application/json")]
public class PresenceController : ControllerBase
{
    private readonly IPresenceService _presenceService;

    public PresenceController(IPresenceService presenceService)
    {
        _presenceService = presenceService;
    }

    /// <summary>
    /// GET /api/presence/online
    /// Returns all online users. No DB round trip.
    /// </summary>
    [HttpGet("online")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<UserPresenceDto>>), 200)]
    public async Task<IActionResult> GetOnlineUsers()
    {
        var users = await _presenceService.GetOnlineUsersDtos();
        return Ok(ApiResponseDto<IList<UserPresenceDto>>.Ok(users));
    }

    /// <summary>
    /// GET /api/presence/{userId}/is-online
    /// IsUserOnline — driven by PresenceService.IsUserOnline(userId).
    /// No DB round trip — ConcurrentDictionary lookup.
    /// </summary>
    [HttpGet("{userId:int}/is-online")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public IActionResult IsUserOnline(int userId)
    {
        var isOnline = _presenceService.IsUserOnline(userId);
        return Ok(ApiResponseDto<object>.Ok(new { userId, isOnline }));
    }

    /// <summary>
    /// GET /api/presence/connections/count
    /// Total active WebSocket connection count. No DB round trip.
    /// </summary>
    [HttpGet("connections/count")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public IActionResult GetConnectionCount()
    {
        var count = _presenceService.GetConnectionCount();
        return Ok(ApiResponseDto<object>.Ok(new { activeConnections = count }));
    }

    /// <summary>
    /// GET /api/presence/online/info
    /// Returns full UserConnection metadata for all active connections.
    /// No DB round trip — reads _userInfo ConcurrentDictionary.
    /// Admin only.
    /// </summary>
    [HttpGet("online/info")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public IActionResult GetOnlineUsersInfo()
    {
        var info = _presenceService.GetOnlineUsersInfo();
        return Ok(ApiResponseDto<object>.Ok(info));
    }

    /// <summary>
    /// GET /api/presence/online/ids
    /// Returns just the list of online user IDs — lightweight.
    /// </summary>
    [HttpGet("online/ids")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<int>>), 200)]
    public IActionResult GetOnlineUserIds()
    {
        var ids = _presenceService.GetOnlineUserIds();
        return Ok(ApiResponseDto<IList<int>>.Ok(ids));
    }

    /// <summary>
    /// GET /api/presence/{userId}/connections
    /// Get all connectionIds for a user (multi-tab/device).
    /// No DB round trip.
    /// </summary>
    [HttpGet("{userId:int}/connections")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public IActionResult GetConnectionsByUserId(int userId)
    {
        var conns = _presenceService.GetConnectionsByUserId(userId);
        return Ok(ApiResponseDto<object>.Ok(new
        {
            userId,
            connectionIds = conns,
            count = conns.Count
        }));
    }

    /// <summary>
    /// POST /api/presence/bulk
    /// Bulk presence for a list of userIds — used by chat sidebar.
    /// No DB round trip — ConcurrentDictionary lookup per userId.
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<UserPresenceDto>>), 200)]
    public async Task<IActionResult> GetBulkPresence([FromBody] BulkPresenceRequestDto dto)
    {
        if (dto.UserIds is null || dto.UserIds.Count == 0)
            return BadRequest(ApiResponseDto<object>.Fail("UserIds list is required."));

        if (dto.UserIds.Count > 500)
            return BadRequest(ApiResponseDto<object>.Fail("Cannot query more than 500 users at once."));

        var presences = await _presenceService.GetPresenceForUsers(dto.UserIds);
        return Ok(ApiResponseDto<IList<UserPresenceDto>>.Ok(presences));
    }
}
