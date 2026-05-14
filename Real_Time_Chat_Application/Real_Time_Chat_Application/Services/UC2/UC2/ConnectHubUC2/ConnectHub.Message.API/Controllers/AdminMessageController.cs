using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Message.Controllers;

/// <summary>
/// AdminMessageController — Admin role only endpoints
/// Routes: GET/DELETE /api/admin/messages
/// Implements admin use cases from use case summary:
///   - View All Rooms / Messages (Admin-scoped GET endpoints)
///   - Delete Room / Message (Admin delete)
///   - Manage Users messages
/// </summary>
[ApiController]
[Route("api/admin/messages")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminMessageController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<AdminMessageController> _logger;

    public AdminMessageController(IMessageService messageService, ILogger<AdminMessageController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/messages/direct?senderId=&amp;receiverId=
    /// Admin can view all direct messages between any two users
    /// "Admin-scoped GET endpoints return all ChatRoom and Message entities platform-wide"
    /// </summary>
    [HttpGet("direct")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MessageDto>>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetDirectMessagesBetweenUsers(
        [FromQuery] int senderId, [FromQuery] int receiverId)
    {
        var messages = await _messageService.AdminGetDirectMessages(senderId, receiverId);
        return Ok(ApiResponseDto<IList<MessageDto>>.Ok(messages));
    }

    /// <summary>
    /// GET /api/admin/messages/room/{roomId}
    /// Admin can view all messages in any room
    /// </summary>
    [HttpGet("room/{roomId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MessageDto>>), 200)]
    public async Task<IActionResult> GetAllRoomMessages(int roomId)
    {
        var messages = await _messageService.AdminGetRoomMessages(roomId);
        return Ok(ApiResponseDto<IList<MessageDto>>.Ok(messages));
    }

    /// <summary>
    /// DELETE /api/admin/messages/{id}
    /// Admin can delete any message from any user — soft delete
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminDeleteMessage(int id)
    {
        await _messageService.AdminDeleteMessage(id);
        _logger.LogWarning("Admin deleted message {MessageId}", id);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Message deleted by admin"));
    }

    /// <summary>
    /// GET /api/admin/messages/{id}
    /// Admin get single message by ID (including soft-deleted for audit)
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMessageById(int id)
    {
        var message = await _messageService.GetMessageById(id);
        if (message is null)
            return NotFound(ApiResponseDto<object>.Fail($"Message {id} not found"));

        return Ok(ApiResponseDto<MessageDto>.Ok(message));
    }
}
