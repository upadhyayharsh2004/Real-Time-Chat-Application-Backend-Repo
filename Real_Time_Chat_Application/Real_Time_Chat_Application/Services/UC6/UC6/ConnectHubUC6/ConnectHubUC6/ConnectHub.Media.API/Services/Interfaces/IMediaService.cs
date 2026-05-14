using ConnectHub.Media.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace ConnectHub.Media.Services.Interfaces;

/// <summary>
/// IMediaService — ConnectHub Media Services
/// Methods exactly as per class diagram (Figure 7):
///   UploadFile(IFormFile, int):MediaFile
///   GetFileById():MediaFile
///   GetFilesByUser():IList&lt;MediaFile&gt;
///   GetFilesByRoom():IList&lt;MediaFile&gt;
///   GetFilesByMessage():IList&lt;MediaFile&gt;
///   DeleteFile():void
///   GenerateSasUrl(BlobSasBuilder):string
///   CleanupExpiredFiles():void  [IHostedService]
///   GetFileStats():Dictionary&lt;string,long&gt;
///
/// MediaService uses Azure.Storage.Blobs BlobServiceClient injected via
/// IOptions&lt;AzureBlobOptions&gt;. UploadFile() streams IFormFile directly to
/// BlobClient.UploadAsync() without temp disk write.
/// GenerateSasUrl() uses BlobSasBuilder with ExpiresOn = DateTime.UtcNow.AddHours(1).
/// </summary>
public interface IMediaService
{
    // ── Class diagram methods ──────────────────────────────────────
    Task<MediaFileDto> UploadFile(IFormFile file, int uploadedByUserId,
        int? messageId = null, int? roomId = null);

    Task<MediaFileDto?> GetFileById(string fileId);
    Task<IList<MediaFileDto>> GetFilesByUser(int userId);
    Task<IList<MediaFileDto>> GetFilesByRoom(int roomId);
    Task<IList<MediaFileDto>> GetFilesByMessage(int messageId);
    Task DeleteFile(string fileId, int requestingUserId, string? role = null);
    Task<SasUrlDto> GenerateSasUrl(string fileId);

    /// <summary>
    /// CleanupExpiredFiles — called by MediaCleanupService (IHostedService) daily.
    /// Deletes blobs from Azure Blob Storage and removes MediaFile records from DB.
    /// </summary>
    Task CleanupExpiredFiles();

    /// <summary>
    /// GetFileStats — returns Dictionary&lt;string, long&gt; with file counts/sizes by type.
    /// GET /api/media/stats — Admin endpoint.
    /// </summary>
    Task<FileStatsDto> GetFileStats();
}
