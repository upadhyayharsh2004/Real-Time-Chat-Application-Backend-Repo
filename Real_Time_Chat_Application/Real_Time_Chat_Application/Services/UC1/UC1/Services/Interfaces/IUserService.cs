// using ConnectHub.Auth.Models.DTOs;
// using ConnectHub.Auth.Models.Entities;

// namespace ConnectHub.Auth.Services.Interfaces;

// /// <summary>
// /// IUserService — ConnectHub Auth Services
// /// Methods exactly as specified in the case study class diagram
// /// </summary>
// public interface IUserService
// {
//     Task<User> Register(User user);
//     Task<AuthResponseDto?> Login(string email, string password);
//     Task Logout(string refreshToken,int userId);
//     Task<bool> ValidateToken(string token);
//     Task<AuthResponseDto?> RefreshToken(string refreshToken,int userId);
//     Task<User?> GetUserById(int userId);
//     Task<User?> GetUserByUserName(string userName);
//     Task<User> UpdateProfile(int userId, User updatedUser);
//     Task ChangePassword(int userId, string currentPassword, string newPassword);
//     Task<IList<User>> SearchUsers(string query);
//     Task SetOnlineStatus(int userId, bool isOnline);
//     Task<IList<User>> GetAllActiveUsers();
//     Task DeactivateAccount(int userId);

//     Task<AuthResponseDto> GenerateTokensForUser(User userId);

//     Task<User?> GetByEmail(string email);
// }



using ConnectHub.Auth.Models.DTOs;
using ConnectHub.Auth.Models.Entities;

namespace ConnectHub.Auth.Services.Interfaces;

/// <summary>
/// IUserService — ConnectHub Auth Services
/// Methods exactly as specified in the case study class diagram
/// </summary>
public interface IUserService
{
    Task<User> Register(User user);
    Task<AuthResponseDto?> Login(string email, string password);
    Task Logout(string refreshToken, int userId, string token);
    Task<bool> ValidateToken(string token);
    Task<AuthResponseDto?> RefreshToken(string refreshToken, int userId);
    Task<User?> GetUserById(int userId);
    Task<User?> GetUserByUserName(string userName);
    Task<User> UpdateProfile(int userId, User updatedUser);
    /// <summary>
    /// Changes password, revokes all refresh tokens, sets IsOnline = false,
    /// and blacklists the current access token so it cannot be reused.
    /// </summary>
    Task ChangePassword(int userId, string currentPassword, string newPassword, string? currentAccessToken = null);
    Task<IList<User>> SearchUsers(string query);
    Task SetOnlineStatus(int userId, bool isOnline);
    Task<IList<User>> GetAllActiveUsers();
    Task DeactivateAccount(int userId, string token);

    Task<AuthResponseDto> GenerateTokensForUser(User userId);

    Task<bool> ChangeUserRole(int userId, string newRole);

    Task<User?> GetByEmail(string email);

    Task<bool> ReactivateUser(int id);


    Task<IList<User>> GetUsersByRole(string role);
    Task<User?> GetUserByEmail(string email);
}
