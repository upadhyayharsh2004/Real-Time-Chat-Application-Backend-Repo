using ConnectHub.Auth.Models.Entities;

namespace ConnectHub.Auth.Repositories.Interfaces;

/// <summary>
/// IUserRepository — ConnectHub Auth Repositories
/// Methods exactly as specified in the case study class diagram
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByEmail(string email);

    Task<IList<User>> FindAllByRole(string role);
    Task<User?> FindByUserId(int userId);
    Task<User?> FindByUserName(string userName);
    Task<bool> ExistsByEmail(string email);
    Task<bool> ExistsByUserName(string userName);
    Task<IList<User>> FindAllActive();
    Task UpdateOnlineStatus(int userId, bool isOnline, DateTime lastSeen);
    Task<IList<User>> SearchUsers(string query);
    Task<User> AddUser(User user);
    Task<User> UpdateUser(User user);
    Task SaveChangesAsync();
}
