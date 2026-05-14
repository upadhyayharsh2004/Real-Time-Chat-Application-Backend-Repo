using System.Security.Claims;
using ConnectHub.Notification.Models.DTOs;
using ConnectHub.Notification.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Notification.Controllers;

/// <summary>
/// NotificationController — [ApiController][Route("api/notifications")]
/// As per class diagram:
///   GetByRecipient(int)  → IActionResult — GET /api/notifications/{userId}
///   GetUnread(int)       → IActionResult — GET /api/notifications/{userId}/unread
///   GetUnreadCount(int)  → IActionResult — GET /api/notifications/{userId}/unread-count
///   MarkAsRead(int)      → IActionResult — PUT /api/notifications/{id}/read
///   MarkAllRead(int)     → IActionResult — PUT /api/notifications/{userId}/read-all
///   DeleteNotification(int) → IActionResult — DELETE /api/notifications/{id}
///   SendBulk(Dictionary) → IActionResult — POST /api/notifications/send-bulk (Admin)
///   GetAll()             → IActionResult — GET /api/notifications/all (Admin)
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/notifications/{userId}
    /// Paginated list of notifications for a user ordered by SentAt descending.
    /// Users can only get their own; Admins can get any.
    /// </summary>
    [HttpGet("{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<NotificationDto>>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetByRecipient(
        int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("You can only view your own notifications."));

        var notifications = await _notificationService.GetByRecipient(userId, page, pageSize);
        return Ok(ApiResponseDto<IList<NotificationDto>>.Ok(notifications));
    }

    /// <summary>
    /// GET /api/notifications/{userId}/unread
    /// All unread notifications for a user.
    /// </summary>
    [HttpGet("{userId:int}/unread")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<NotificationDto>>), 200)]
    public async Task<IActionResult> GetUnread(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("Forbidden."));

        var notifications = await _notificationService.GetUnread(userId);
        return Ok(ApiResponseDto<IList<NotificationDto>>.Ok(notifications));
    }

    /// <summary>
    /// GET /api/notifications/{userId}/unread-count
    /// COUNT of unread notifications.
    /// Used by client to display badge count (REST fallback; SignalR is primary).
    /// </summary>
    [HttpGet("{userId:int}/unread-count")]
    [ProducesResponseType(typeof(ApiResponseDto<UnreadCountDto>), 200)]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("Forbidden."));

        var count = await _notificationService.GetUnreadCount(userId);
        return Ok(ApiResponseDto<UnreadCountDto>.Ok(new UnreadCountDto
        {
            RecipientId = userId,
            UnreadCount = count
        }));
    }

    /// <summary>
    /// PUT /api/notifications/{id}/read
    /// Mark single notification as read.
    /// Also pushes updated badge count via SignalR from service.
    /// </summary>
    [HttpPut("{id:int}/read")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsRead(id);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Notification marked as read."));
    }

    /// <summary>
    /// PUT /api/notifications/{userId}/read-all
    /// Mark all notifications as read for a user.
    /// Also pushes NotificationCount=0 via SignalR.
    /// </summary>
    [HttpPut("{userId:int}/read-all")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> MarkAllRead(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("Forbidden."));

        await _notificationService.MarkAllRead(userId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "All notifications marked as read."));
    }

    /// <summary>
    /// DELETE /api/notifications/{id}
    /// Delete a single notification.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        await _notificationService.DeleteNotification(id);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Notification deleted."));
    }

    /// <summary>
    /// POST /api/notifications/send-bulk
    /// Admin broadcasts PLATFORM notification to all or specific users.
    /// SendBulk(Dictionary) as per class diagram.
    /// </summary>
    [HttpPost("send-bulk")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendBulk([FromBody] SendBulkRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        if (!dto.RecipientIds.Any())
            return BadRequest(ApiResponseDto<object>.Fail("At least one recipient is required."));

        await _notificationService.SendBulk(dto.RecipientIds, dto.Title, dto.Message);
        return Ok(ApiResponseDto<object>.Ok(new { count = dto.RecipientIds.Count },
            $"Bulk notification sent to {dto.RecipientIds.Count} user(s)."));
    }

    /// <summary>
    /// GET /api/notifications/all
    /// Admin: get all notifications platform-wide (paginated).
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<NotificationDto>>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var notifications = await _notificationService.GetAll(page, pageSize);
        return Ok(ApiResponseDto<IList<NotificationDto>>.Ok(notifications));
    }

    // ── Helper ─────────────────────────────────────────────────────

    private int GetCallerId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (claim is null || !int.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("Unable to determine caller identity.");

        return id;
    }

    private string GetCallerRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
}
