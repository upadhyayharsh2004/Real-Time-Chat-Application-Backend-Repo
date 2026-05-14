using ConnectHub.Auth.Data;
using ConnectHub.Auth.Models.Entities;
using ConnectHub.Auth.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Auth.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _context;

    public UserRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<User?> FindByEmail(string email) =>
        await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());

    public async Task<User?> FindByUserId(int userId) =>
        await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.UserId == userId);

    public async Task<User?> FindByUserName(string userName) =>
        await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == userName);

    public async Task<bool> ExistsByEmail(string email) =>
        await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email);

    public async Task<bool> ExistsByUserName(string userName) =>
        await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserName == userName);

    public async Task<IList<User>> FindAllActive() =>
        await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

    public async Task UpdateOnlineStatus(int userId, bool isOnline, DateTime lastSeen)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null) return;

        user.IsOnline = isOnline;
        user.LastSeen = lastSeen;

        await _context.SaveChangesAsync();
    }

    // public async Task<IList<User>> SearchUsers(string query) =>
    //     await _context.Users
    //         .AsNoTracking()
    //         .Where(u => u.IsActive &&
    //         (EF.Functions.Like(u.UserName.ToLower(), $"%{query.ToLower()}%") ||
    //         EF.Functions.Like(u.DisplayName.ToLower(), $"%{query.ToLower()}%")))
    //         .OrderBy(u => u.UserName)
    //         .Take(20)
    //         .ToListAsync();


    public async Task<IList<User>> SearchUsers(string query) =>
    await _context.Users
        .AsNoTracking()
        .Where(u =>
            (EF.Functions.Like(u.UserName.ToLower(), $"%{query.ToLower()}%") ||
             EF.Functions.Like(u.DisplayName.ToLower(), $"%{query.ToLower()}%"))
        )
        .ToListAsync();

    public async Task<IList<User>> FindAllByRole(string role)
    {
        if(role=="Admin")
        {
            return await _context.Users
            .Where(u => u.Role == role  && u.IsActive)
            .OrderBy(u => u.UserName)
            .ToListAsync();
        }
        return await _context.Users
            .Where(u => u.Role == role)
            .OrderBy(u => u.UserName)
            .ToListAsync();
    }

    public async Task<User> AddUser(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateUser(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}