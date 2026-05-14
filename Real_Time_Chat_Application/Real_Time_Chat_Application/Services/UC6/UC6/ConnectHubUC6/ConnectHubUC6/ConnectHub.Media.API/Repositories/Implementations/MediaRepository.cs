using ConnectHub.Media.Data;
using ConnectHub.Media.Models.Entities;
using ConnectHub.Media.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Media.Repositories.Implementations;

/// <summary>
/// MediaRepository — EF Core implementation of IMediaRepository.
/// Uses indexes: (UploadedBy), (RoomId), (MessageId), (ExpiresAt) for performance.
/// </summary>
public class MediaRepository : IMediaRepository
{
    private readonly MediaDbContext _context;

    public MediaRepository(MediaDbContext context)
    {
        _context = context;
    }

    public async Task<MediaFile?> FindByFileId(string fileId) =>
        await _context.MediaFiles.FirstOrDefaultAsync(m => m.FileId == fileId);

    public async Task<IList<MediaFile>> FindByUploadedBy(int userId) =>
        await _context.MediaFiles
            .AsNoTracking()
            .Where(m => m.UploadedBy == userId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

    public async Task<IList<MediaFile>> FindByMessageId(int messageId) =>
        await _context.MediaFiles
            .AsNoTracking()
            .Where(m => m.MessageId == messageId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

    public async Task<IList<MediaFile>> FindByRoomId(int roomId) =>
        await _context.MediaFiles
            .AsNoTracking()
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

    public async Task DeleteByFileId(string fileId)
    {
        var file = await _context.MediaFiles.FindAsync(fileId);
        if (file is null) return;
        _context.MediaFiles.Remove(file);
        await _context.SaveChangesAsync();
    }

    public async Task<IList<MediaFile>> FindExpiredFiles(DateTime cutoffDate) =>
        await _context.MediaFiles
            .AsNoTracking()
            .Where(m => m.ExpiresAt.HasValue && m.ExpiresAt <= cutoffDate)
            .ToListAsync();

    public async Task<MediaFile> Add(MediaFile mediaFile)
    {
        await _context.MediaFiles.AddAsync(mediaFile);
        await _context.SaveChangesAsync();
        return mediaFile;
    }

    public async Task<MediaFile> Update(MediaFile mediaFile)
    {
        _context.MediaFiles.Update(mediaFile);
        await _context.SaveChangesAsync();
        return mediaFile;
    }

    public async Task<int> CountByUser(int userId) =>
        await _context.MediaFiles.AsNoTracking().CountAsync(m => m.UploadedBy == userId);

    public async Task<long> TotalSizeKbByUser(int userId) =>
        await _context.MediaFiles
            .AsNoTracking()
            .Where(m => m.UploadedBy == userId)
            .SumAsync(m => m.FileSizeKb);

    public async Task MarkRoomFilesExpired(int roomId, DateTime expiresAt)
    {
        var files = await _context.MediaFiles
            .Where(m => m.RoomId == roomId && !m.ExpiresAt.HasValue)
            .ToListAsync();

        foreach (var f in files)
            f.ExpiresAt = expiresAt;

        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
