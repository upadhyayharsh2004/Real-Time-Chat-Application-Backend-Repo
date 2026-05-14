using ConnectHub.Auth.Data;
using System.Text;
using System.Security.Cryptography;
using ConnectHub.Auth.Models.Entities;
using ConnectHub.Auth.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Auth.Repositories.Implementations;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context)
    {
        _context = context;
    }
    public static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public async Task<RefreshToken?> FindByToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        return await _context.RefreshTokens
            .AsNoTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<IList<RefreshToken>> FindActiveByUserId(int userId) =>
        await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

    public async Task AddToken(RefreshToken token)
    {
        await _context.RefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeToken(string token)
    {
        var rt = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token);
        if (rt is null) return;
        rt.IsRevoked = true;
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllUserTokens(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens) t.IsRevoked = true;
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
