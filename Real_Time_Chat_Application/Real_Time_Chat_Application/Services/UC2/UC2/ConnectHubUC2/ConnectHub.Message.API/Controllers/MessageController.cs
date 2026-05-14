using System.Security.Claims;
using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Message.Controllers;

/// <summary>
/// MessageController — REST endpoints for message history, search, edit, delete,
/// unread count, recent chats, reactions, and pinned messages.
///
/// Message SENDING is done via SignalR (ChatHub.SendDirectMessage / SendRoomMessage).
/// ChatHub calls MessageService.SendMessage() → DB save → RabbitMQ publish → SignalR push.
/// There is intentionally no POST endpoint here for sending messages.
/// </summary>
[ApiController]
[Route("api/messages")]
[Authorize]
[Produces("application/json")]
public class MessageController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessageController> _logger;

    public MessageController(IMessageService messageService, ILogger<MessageController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    // ── Direct message history ────────────────────────────────────

    /// <summary>GET /api/messages/direct/{userId} — paginated, sorted by SentAt</summary>
    [HttpGet("direct/{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedMessagesDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetDirectMessages(
        int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var callerId = GetCallerId();
        var result = await _messageService.GetDirectMessagesPaged(callerId, userId, page, pageSize);
        return Ok(ApiResponseDto<PagedMessagesDto>.Ok(result));
    }

    /// <summary>GET /api/messages/room/{roomId} — paginated room messages sorted by SentAt</summary>
    [HttpGet("room/{roomId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedMessagesDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetRoomMessages(
        int roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _messageService.GetRoomMessagesPaged(roomId, page, pageSize);
        return Ok(ApiResponseDto<PagedMessagesDto>.Ok(result));
    }

    // ── Unread messages ───────────────────────────────────────────

    /// <summary>GET /api/messages/unread — all unread direct messages for caller</summary>
    [HttpGet("unread")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MessageDto>>), 200)]
    public async Task<IActionResult> GetUnreadMessages()
    {
        var callerId = GetCallerId();
        var messages = await _messageService.GetUnreadMessages(callerId);
        return Ok(ApiResponseDto<IList<MessageDto>>.Ok(messages));
    }

    /// <summary>GET /api/messages/unread-count/{userId} — unread count (users own; admins any)</summary>
    [HttpGet("unread-count/{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<UnreadCountDto>), 200)]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return Forbid();

        var count = await _messageService.GetUnreadCount(userId);
        return Ok(ApiResponseDto<UnreadCountDto>.Ok(new UnreadCountDto { UnreadCount = count }));
    }

    /// <summary>GET /api/messages/unread-count/{userId}/from/{senderId}</summary>
    [HttpGet("unread-count/{userId:int}/from/{senderId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<UnreadCountDto>), 200)]
    public async Task<IActionResult> GetUnreadCountFromSender(int userId, int senderId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return Forbid();

        var count = await _messageService.GetUnreadCountFromSender(userId, senderId);
        return Ok(ApiResponseDto<UnreadCountDto>.Ok(new UnreadCountDto { UnreadCount = count }));
    }

    // ── Mark read ─────────────────────────────────────────────────

    /// <summary>PUT /api/messages/{id}/read — REST fallback (SignalR is primary path)</summary>
    [HttpPut("{id:int}/read")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var callerId = GetCallerId();
        await _messageService.MarkAsRead(id, callerId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Message marked as read"));
    }

    /// <summary>PUT /api/messages/read-all/{senderId} — mark all from sender as read</summary>
    [HttpPut("read-all/{senderId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> MarkAllAsRead(int senderId)
    {
        var callerId = GetCallerId();
        await _messageService.MarkAllAsRead(senderId, callerId);
        return Ok(ApiResponseDto<object>.Ok(new { }, "All messages marked as read"));
    }

    // ── Edit / Delete ─────────────────────────────────────────────

    /// <summary>PUT /api/messages/{id} — update content; sets IsEdited=true, EditedAt=now</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EditMessage(int id, [FromBody] EditMessageRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var callerId = GetCallerId();
        var updated = await _messageService.EditMessage(id, request.Content, callerId);
        return Ok(ApiResponseDto<MessageDto>.Ok(updated, "Message updated"));
    }

    /// <summary>DELETE /api/messages/{id} — soft delete; users own, admins any</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var callerId = GetCallerId();
        var role = GetCallerRole();
        await _messageService.DeleteMessage(id, callerId, role);
        return Ok(ApiResponseDto<object>.Ok(new { }, "Message deleted"));
    }

    // ── Search ────────────────────────────────────────────────────

    /// <summary>GET /api/messages/search?q=keyword — EF LIKE on caller's conversations</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MessageDto>>), 200)]
    public async Task<IActionResult> SearchMessages([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponseDto<object>.Fail("Search query is required"));

        var callerId = GetCallerId();
        var results = await _messageService.SearchMessages(q, callerId);
        return Ok(ApiResponseDto<IList<MessageDto>>.Ok(results));
    }

    // ── Recent chats ──────────────────────────────────────────────

    /// <summary>GET /api/messages/recent — most recent message per conversation partner</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<RecentChatDto>>), 200)]
    public async Task<IActionResult> GetRecentChats()
    {
        var callerId = GetCallerId();
        var chats = await _messageService.GetRecentChats(callerId);
        return Ok(ApiResponseDto<IList<RecentChatDto>>.Ok(chats));
    }

    // ── Single message ────────────────────────────────────────────

    /// <summary>GET /api/messages/{id}</summary>
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

    // ── Reactions ─────────────────────────────────────────────────

    /// <summary>POST /api/messages/{id}/reactions — add emoji reaction</summary>
    [HttpPost("{id:int}/reactions")]
    [ProducesResponseType(typeof(ApiResponseDto<ReactionDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddReaction(int id, [FromBody] AddReactionRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<object>.Fail("Validation failed"));

        var callerId = GetCallerId();
        var reaction = await _messageService.AddReaction(id, callerId, request.Emoji);
        return StatusCode(201, ApiResponseDto<ReactionDto>.Ok(reaction, "Reaction added"));
    }

    /// <summary>DELETE /api/messages/{id}/reactions/{emoji}</summary>
    [HttpDelete("{id:int}/reactions/{emoji}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> RemoveReaction(int id, string emoji)
    {
        var callerId = GetCallerId();
        var removed = await _messageService.RemoveReaction(id, callerId, emoji);
        if (!removed)
            return NotFound(ApiResponseDto<object>.Fail("Reaction not found"));

        return Ok(ApiResponseDto<object>.Ok(new { }, "Reaction removed"));
    }

    /// <summary>GET /api/messages/{id}/reactions</summary>
    [HttpGet("{id:int}/reactions")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<ReactionDto>>), 200)]
    public async Task<IActionResult> GetReactions(int id)
    {
        var reactions = await _messageService.GetReactions(id);
        return Ok(ApiResponseDto<IList<ReactionDto>>.Ok(reactions));
    }

    // ── Pinned messages ───────────────────────────────────────────

    /// <summary>POST /api/messages/{id}/pin</summary>
    [HttpPost("{id:int}/pin")]
    [ProducesResponseType(typeof(ApiResponseDto<PinnedMessageDto>), 201)]
    public async Task<IActionResult> PinMessage(int id, [FromBody] PinMessageRequestDto request)
    {
        var callerId = GetCallerId();
        var pinned = await _messageService.PinMessage(
            id, callerId, request.ConversationWithUserId, request.RoomId);

        return StatusCode(201, ApiResponseDto<PinnedMessageDto>.Ok(pinned, "Message pinned"));
    }

    /// <summary>DELETE /api/messages/pins/{pinId}</summary>
    [HttpDelete("pins/{pinId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    public async Task<IActionResult> UnpinMessage(int pinId)
    {
        var callerId = GetCallerId();
        var removed = await _messageService.UnpinMessage(pinId, callerId);
        if (!removed)
            return NotFound(ApiResponseDto<object>.Fail("Pin not found"));

        return Ok(ApiResponseDto<object>.Ok(new { }, "Message unpinned"));
    }

    /// <summary>GET /api/messages/pins?userId={userId}&amp;roomId={roomId}</summary>
    [HttpGet("pins")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<PinnedMessageDto>>), 200)]
    public async Task<IActionResult> GetPinnedMessages(
        [FromQuery] int? userId, [FromQuery] int? roomId)
    {
        var pins = await _messageService.GetPinnedMessages(userId, roomId);
        return Ok(ApiResponseDto<IList<PinnedMessageDto>>.Ok(pins));
    }

    // ── Test-send (Swagger/demo only) ─────────────────────────────

    /// <summary>
    /// POST /api/messages/test-send
    /// TEMPORARY: Swagger-only test endpoint for sending a message when frontend
    /// SignalR is not available yet. This does NOT replace SignalR; it is for
    /// testing/demo to Swagger only. Saves to DB and publishes RabbitMQ event
    /// exactly like ChatHub.SendDirectMessage / ChatHub.SendRoomMessage would.
    /// </summary>
    [HttpPost("test-send")]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestSend([FromBody] TestSendRequestDto request)
    {
        var callerId = GetCallerId();

        if (request.ReceiverId == null && request.RoomId == null)
            return BadRequest(ApiResponseDto<object>.Fail("Either ReceiverId or RoomId must be provided."));

        if (request.ReceiverId != null && request.RoomId != null)
            return BadRequest(ApiResponseDto<object>.Fail("Only one of ReceiverId or RoomId can be set."));

        var message = new ConnectHub.Message.Models.Entities.Message
        {
            SenderId   = callerId,
            ReceiverId = request.ReceiverId,
            RoomId     = request.RoomId,
            Content    = request.Content,
            MessageType = request.MessageType ?? "TEXT",
            MediaUrl   = request.MediaUrl,
            ReplyToMessageId = request.ReplyToMessageId
        };

        var saved = await _messageService.SendMessage(message);
        return StatusCode(201, ApiResponseDto<MessageDto>.Ok(saved, "Message sent via test endpoint."));
    }

    // ── Helpers ───────────────────────────────────────────────────

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
