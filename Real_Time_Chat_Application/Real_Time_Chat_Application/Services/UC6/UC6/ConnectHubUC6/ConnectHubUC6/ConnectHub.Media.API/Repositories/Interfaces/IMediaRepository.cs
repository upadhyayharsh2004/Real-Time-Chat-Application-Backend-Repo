using ConnectHub.Media.Models.Entities;

namespace ConnectHub.Media.Repositories.Interfaces;

/// <summary>
/// IMediaRepository — ConnectHub Media Repositories
/// Methods exactly as per class diagram (Figure 7):
///   FindByFileId(), FindByUploadedBy(), FindByMessageId(),
///   FindByRoomId(), DeleteByFileId(), FindExpiredFiles(DateTime) — all Task&lt;T&gt;
/// </summary>
public interface IMediaRepository
{
    Task<MediaFile?> FindByFileId(string fileId);
    Task<IList<MediaFile>> FindByUploadedBy(int userId);
    Task<IList<MediaFile>> FindByMessageId(int messageId);
    Task<IList<MediaFile>> FindByRoomId(int roomId);
    Task DeleteByFileId(string fileId);
    Task<IList<MediaFile>> FindExpiredFiles(DateTime cutoffDate);

    // ── Extended ───────────────────────────────────────────────────
    Task<MediaFile> Add(MediaFile mediaFile);
    Task<MediaFile> Update(MediaFile mediaFile);
    Task<int> CountByUser(int userId);
    Task<long> TotalSizeKbByUser(int userId);
    Task MarkRoomFilesExpired(int roomId, DateTime expiresAt);
    Task SaveChangesAsync();
}
