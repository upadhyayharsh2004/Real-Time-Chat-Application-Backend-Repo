using System.Security.Claims;
using ConnectHub.Media.Models.DTOs;
using ConnectHub.Media.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectHub.Media.Controllers;

/// <summary>
/// MediaController — [ApiController][Route("api/media")]
/// As per class diagram:
///   POST   /api/media/upload            → UploadFile
///   GET    /api/media/{fileId}          → GetById
///   GET    /api/media/by-user/{userId}  → GetFilesByUser
///   GET    /api/media/by-room/{roomId}  → GetFilesByRoom
///   GET    /api/media/by-message/{id}   → GetFilesByMessage
///   GET    /api/media/{fileId}/sas-url  → GenerateSasUrl
///   DELETE /api/media/{fileId}          → DeleteFile
///   GET    /api/media/stats             → GetFileStats (Admin)
/// </summary>
[ApiController]
[Route("api/media")]
[Authorize]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IMediaService mediaService, ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/media/upload
    /// Uploads IFormFile to Azure Blob Storage (no temp disk write).
    /// Stores MediaFile metadata in SQL Server.
    /// Publishes MediaUploadedEvent to RabbitMQ AFTER DB save.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponseDto<MediaFileDto>), 201)]
    [ProducesResponseType(400)]
    [RequestSizeLimit(104_857_600)] // 100MB
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromQuery] int? messageId = null,
        [FromQuery] int? roomId = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponseDto<object>.Fail("No file provided."));

        var callerId = GetCallerId();
        var result = await _mediaService.UploadFile(file, callerId, messageId, roomId);

        return StatusCode(201, ApiResponseDto<MediaFileDto>.Ok(result, "File uploaded successfully."));
    }

    /// <summary>
    /// GET /api/media/{fileId}
    /// Get MediaFile metadata by FileId.
    /// </summary>
    [HttpGet("{fileId}")]
    [ProducesResponseType(typeof(ApiResponseDto<MediaFileDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(string fileId)
    {
        var file = await _mediaService.GetFileById(fileId);
        if (file is null)
            return NotFound(ApiResponseDto<object>.Fail($"File {fileId} not found."));

        return Ok(ApiResponseDto<MediaFileDto>.Ok(file));
    }

    /// <summary>
    /// GET /api/media/by-user/{userId}
    /// Get all files uploaded by a specific user.
    /// Users can only get their own; Admins can get any.
    /// </summary>
    [HttpGet("by-user/{userId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByUser(int userId)
    {
        var callerId = GetCallerId();
        if (callerId != userId && GetCallerRole() != "Admin")
            return StatusCode(403, ApiResponseDto<object>.Fail("You can only view your own files."));

        var files = await _mediaService.GetFilesByUser(userId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }

    /// <summary>
    /// GET /api/media/by-room/{roomId}
    /// Get all files in a room (media gallery).
    /// Filtered by RoomId ordered by UploadedAt.
    /// </summary>
    [HttpGet("by-room/{roomId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByRoom(int roomId)
    {
        var files = await _mediaService.GetFilesByRoom(roomId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }

    /// <summary>
    /// GET /api/media/by-message/{messageId}
    /// Get all files attached to a specific message.
    /// </summary>
    [HttpGet("by-message/{messageId:int}")]
    [ProducesResponseType(typeof(ApiResponseDto<IList<MediaFileDto>>), 200)]
    public async Task<IActionResult> GetFilesByMessage(int messageId)
    {
        var files = await _mediaService.GetFilesByMessage(messageId);
        return Ok(ApiResponseDto<IList<MediaFileDto>>.Ok(files));
    }

    /// <summary>
    /// GET /api/media/{fileId}/sas-url
    /// Generates a time-limited SAS URL (ExpiresOn = UtcNow.AddHours(1)).
    /// BlobSasBuilder — secure download without a public Blob container.
    /// </summary>
    [HttpGet("{fileId}/sas-url")]
    [ProducesResponseType(typeof(ApiResponseDto<SasUrlDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSasUrl(string fileId)
    {
        var sasUrl = await _mediaService.GenerateSasUrl(fileId);
        return Ok(ApiResponseDto<SasUrlDto>.Ok(sasUrl));
    }

    /// <summary>
    /// DELETE /api/media/{fileId}
    /// Deletes blob from Azure Blob Storage and MediaFile from DB.
    /// Users: own files only. Admins: any file.
    /// Publishes MediaDeletedEvent to RabbitMQ.
    /// </summary>
    [HttpDelete("{fileId}")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteFile(string fileId)
    {
        var callerId = GetCallerId();
        var role = GetCallerRole();
        await _mediaService.DeleteFile(fileId, callerId, role);
        return Ok(ApiResponseDto<object>.Ok(new { }, "File deleted successfully."));
    }

    /// <summary>
    /// GET /api/media/stats
    /// Admin: file counts and sizes by content type.
    /// GetFileStats() as per class diagram.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponseDto<FileStatsDto>), 200)]
    public async Task<IActionResult> GetFileStats()
    {
        var stats = await _mediaService.GetFileStats();
        return Ok(ApiResponseDto<FileStatsDto>.Ok(stats));
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
