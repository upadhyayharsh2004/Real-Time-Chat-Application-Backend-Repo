// using ConnectHub.Auth.Models.DTOs;
// using ConnectHub.Auth.Models.Entities;
// using ConnectHub.Auth.Repositories.Interfaces;
// using ConnectHub.Auth.Services.Interfaces;
// using Microsoft.AspNetCore.Identity;
// using ConnectHub.Auth.Data;

// namespace ConnectHub.Auth.Services.Implementations;

// /// <summary>
// /// UserService — implements IUserService
// /// Uses PasswordHasher&lt;User&gt; for bcrypt hashing (ASP.NET Core Identity)
// /// JWT tokens issued on Login via IJwtService
// /// ConnectHub Auth Services
// /// </summary>
// public class UserService : IUserService
// {
//     private readonly IUserRepository _userRepo;
//     private readonly IRefreshTokenRepository _refreshTokenRepo;
//     private readonly IJwtService _jwtService;
//     private readonly IPasswordHasher<User> _passwordHasher;
//     private readonly IConfiguration _config;
//     private readonly ILogger<UserService> _logger;

//     private readonly AuthDbContext _context;

//     public UserService(
//         IUserRepository userRepo,
//         IRefreshTokenRepository refreshTokenRepo,
//         IJwtService jwtService,
//         IPasswordHasher<User> passwordHasher,
//         IConfiguration config,
//         ILogger<UserService> logger)
//     {
//         _userRepo = userRepo;
//         _refreshTokenRepo = refreshTokenRepo;
//         _jwtService = jwtService;
//         _passwordHasher = passwordHasher;
//         _config = config;
//         _logger = logger;
//     }

//     /// <summary>
//     /// Register — creates new user account
//     /// Validates unique email and username before saving
//     /// </summary>
//     public async Task<User> Register(User user)
//     {
//         if (await _userRepo.ExistsByEmail(user.Email))
//             throw new InvalidOperationException("Email is already registered.");

//         if (await _userRepo.ExistsByUserName(user.UserName))
//             throw new InvalidOperationException("Username is already taken.");

//         // bcrypt hash via PasswordHasher<User> (per document)
//         user.PasswordHash = _passwordHasher.HashPassword(user, user.PasswordHash);
//         user.CreatedAt = DateTime.UtcNow;
//         user.IsActive = true;

//         var created = await _userRepo.AddUser(user);
//         _logger.LogInformation("New user registered: {UserName} ({Email})",
//             created.UserName, created.Email);
//         return created;
//     }

//     /// <summary>
//     /// Login — returns JWT access token + refresh token on success
//     /// ChatHub uses Context.UserIdentifier (mapped to UserId sub claim)
//     /// </summary>
//     public async Task<AuthResponseDto?> Login(string email, string password)
//     {
//         var user = await _userRepo.FindByEmail(email);
//         if (user is null || !user.IsActive)
//             return null;

//         var result = _passwordHasher.VerifyHashedPassword(
//             user, user.PasswordHash, password);

//         if (result == PasswordVerificationResult.Failed)
//             return null;

//         // Update IsOnline = true on login
//         await _userRepo.UpdateOnlineStatus(user.UserId, true, DateTime.UtcNow);

//         user.IsOnline = true;
//         user.LastSeen = DateTime.UtcNow;

//         var accessToken = _jwtService.GenerateAccessToken(user);
//         var refreshTokenString = _jwtService.GenerateRefreshToken();
//         var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

//         var refreshToken = new RefreshToken
//         {
//             Token = refreshTokenString,
//             UserId = user.UserId,
//             ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry),
//             CreatedAt = DateTime.UtcNow
//         };

//         await _refreshTokenRepo.AddToken(refreshToken);

//         _logger.LogInformation("User logged in: {UserName}", user.UserName);

//         return new AuthResponseDto
//         {
//             AccessToken = accessToken,
//             RefreshToken = refreshTokenString,
//             ExpiresAt = DateTime.UtcNow.AddMinutes(
//                 int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
//             User = MapToProfileDto(user)
//         };
//     }
//     public async Task<AuthResponseDto> GenerateTokensForUser(User user)
//     {
//         //Generate JWT
//         var accessToken = _jwtService.GenerateAccessToken(user);

//         // 🔄 Generate Refresh Token
//         var refreshToken = _jwtService.GenerateRefreshToken();

//         var refreshTokenEntity = new RefreshToken
//         {
//             Token = refreshToken,
//             UserId = user.UserId,
//             ExpiresAt = DateTime.UtcNow.AddDays(
//                 int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7")
//             ),
//             CreatedAt = DateTime.UtcNow
//         };

//         await _refreshTokenRepo.AddToken(refreshTokenEntity);

//         //Update online status
//         await _userRepo.UpdateOnlineStatus(user.UserId, true, DateTime.UtcNow);

//         user.IsOnline = true;
//         user.LastSeen = DateTime.UtcNow;

//         return new AuthResponseDto
//         {
//             AccessToken = accessToken,
//             RefreshToken = refreshToken,
//             ExpiresAt = DateTime.UtcNow.AddMinutes(
//                 int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")
//             ),
//             User = MapToProfileDto(user)
//         };
//     }
//     /// <summary>
//     /// Logout — revokes all refresh tokens + sets IsOnline = false
//     /// </summary>
//     public async Task Logout(string refreshToken, int currentUserId)
//     {
//         var tokenEntity = await _refreshTokenRepo.FindByToken(refreshToken);

//         if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.IsExpired)
//             throw new UnauthorizedAccessException("Invalid refresh token");


//         if (tokenEntity.UserId != currentUserId)
//             throw new UnauthorizedAccessException("This token does not belong to you");

//         await _refreshTokenRepo.RevokeToken(refreshToken);

//         await _userRepo.UpdateOnlineStatus(currentUserId, false, DateTime.UtcNow);

//         _logger.LogInformation("User logged out: UserId={UserId}", currentUserId);
//     }

//     public async Task<bool> ValidateToken(string token) =>
//         await Task.FromResult(_jwtService.ValidateToken(token));

//     /// <summary>
//     /// RefreshToken — rotates refresh token (revoke old, issue new)
//     /// </summary>
//     public async Task<AuthResponseDto?> RefreshToken(string refreshToken, int currentUserId)
//     {
//         var stored = await _refreshTokenRepo.FindByToken(refreshToken);
//         if (stored is null || !stored.IsActive)
//             return null;

//         if (stored.UserId != currentUserId)
//             return null;

//         var user = stored.User;
//         if (!user.IsActive) return null;

//         // Revoke old token
//         await _refreshTokenRepo.RevokeToken(refreshToken);

//         // Issue new tokens
//         var newAccessToken = _jwtService.GenerateAccessToken(user);
//         var newRefreshToken = _jwtService.GenerateRefreshToken();
//         var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

//         await _refreshTokenRepo.AddToken(new RefreshToken
//         {
//             Token = newRefreshToken,
//             UserId = user.UserId,
//             ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry)
//         });

//         return new AuthResponseDto
//         {
//             AccessToken = newAccessToken,
//             RefreshToken = newRefreshToken,
//             ExpiresAt = DateTime.UtcNow.AddMinutes(
//                 int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
//             User = MapToProfileDto(user)
//         };
//     }

//     public async Task<User?> GetByEmail(string email)
//     {
//         email = email.ToLower().Trim();
//         return await _userRepo.FindByEmail(email);
//     }

//     public async Task<User?> GetUserById(int userId)
//     {
//         var user = await _userRepo.FindByUserId(userId);

//         if (user == null || !user.IsActive)
//             return null;

//         return user;
//     }

//     public async Task<User?> GetUserByUserName(string userName) =>
//         await _userRepo.FindByUserName(userName);

//     public async Task<User> UpdateProfile(int userId, User updatedUser)
//     {
//         var user = await _userRepo.FindByUserId(userId)
//             ?? throw new KeyNotFoundException($"User {userId} not found.");

//         if (!string.IsNullOrWhiteSpace(updatedUser.DisplayName))
//             user.DisplayName = updatedUser.DisplayName;
//         if (!string.IsNullOrWhiteSpace(updatedUser.Bio))
//             user.Bio = updatedUser.Bio;
//         if (!string.IsNullOrWhiteSpace(updatedUser.AvatarUrl))
//             user.AvatarUrl = updatedUser.AvatarUrl;

//         return await _userRepo.UpdateUser(user);
//     }

//     public async Task ChangePassword(int userId, string currentPassword, string newPassword)
//     {
//         var user = await _userRepo.FindByUserId(userId)
//             ?? throw new KeyNotFoundException($"User {userId} not found.");

//         if (!user.IsActive)
//             throw new UnauthorizedAccessException("Account is deactivated");

//         var result = _passwordHasher.VerifyHashedPassword(
//             user, user.PasswordHash, currentPassword);

//         if (result == PasswordVerificationResult.Failed)
//             throw new UnauthorizedAccessException("Current password is incorrect.");

//         user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
//         await _userRepo.UpdateUser(user);

//         // Revoke all refresh tokens on password change for security
//         await _refreshTokenRepo.RevokeAllUserTokens(userId);
//         _logger.LogInformation("Password changed for UserId={UserId}", userId);
//     }

//     /// <summary>
//     /// SearchUsers — full-text search on UserName and DisplayName
//     /// per document: UserController.SearchUsers()
//     /// </summary>
//     public async Task<IList<User>> SearchUsers(string query)
//     {
//         var users = await _userRepo.SearchUsers(query);

//         return users.Where(u => u.IsActive).ToList();
//     }

//     /// <summary>
//     /// SetOnlineStatus — called from ChatHub lifecycle events
//     /// OnConnectedAsync and OnDisconnectedAsync
//     /// </summary>
//     public async Task SetOnlineStatus(int userId, bool isOnline) =>
//         await _userRepo.UpdateOnlineStatus(userId, isOnline, DateTime.UtcNow);

//     public async Task<IList<User>> GetAllActiveUsers() =>
//         await _userRepo.FindAllActive();

//     /// <summary>
//     /// DeactivateAccount — IsActive = false prevents login
//     /// without deleting message history (per document requirement)
//     /// </summary>
//     public async Task DeactivateAccount(int userId)
//     {
//         var user = await _userRepo.FindByUserId(userId)
//             ?? throw new KeyNotFoundException($"User {userId} not found.");

//         user.IsOnline = false;
//         user.LastSeen = DateTime.UtcNow;

//         user.IsActive = false;
//         await _userRepo.UpdateUser(user);
//         await _refreshTokenRepo.RevokeAllUserTokens(userId);
//         _logger.LogWarning("Account deactivated: UserId={UserId}", userId);
//     }

//     // ── Mapping helper ──────────────────────────────────────────
//     private static UserProfileDto MapToProfileDto(User user) => new()
//     {
//         UserId = user.UserId,
//         UserName = user.UserName,
//         DisplayName = user.DisplayName,
//         Email = user.Email,
//         AvatarUrl = user.AvatarUrl,
//         Bio = user.Bio,
//         IsOnline = user.IsOnline,
//         LastSeen = user.LastSeen,
//         CreatedAt = user.CreatedAt,
//         IsActive = user.IsActive
//     };
// }



using System.IdentityModel.Tokens.Jwt;
using ConnectHub.Auth.Models.DTOs;
using ConnectHub.Auth.Models.Entities;
using ConnectHub.Auth.Models.Events;
using ConnectHub.Auth.Repositories.Interfaces;
using ConnectHub.Auth.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ConnectHub.Auth.Data;

namespace ConnectHub.Auth.Services.Implementations;

/// <summary>
/// UserService — implements IUserService
/// Uses PasswordHasher<User> for bcrypt hashing (ASP.NET Core Identity)
/// JWT tokens issued on Login via IJwtService
/// ConnectHub Auth Services
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _config;
    private readonly ILogger<UserService> _logger;
    private readonly ITokenBlacklistService _tokenBlacklist;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;

    private readonly AuthDbContext _context;

    public UserService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtService jwtService,
        IPasswordHasher<User> passwordHasher,
        IConfiguration config,
        ILogger<UserService> logger,
        ITokenBlacklistService tokenBlacklist,
        IRabbitMqPublisher rabbitMqPublisher)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _config = config;
        _logger = logger;
        _tokenBlacklist = tokenBlacklist;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    /// <summary>
    /// Register — creates new user account
    /// Validates unique email and username before saving
    /// </summary>
    public async Task<User> Register(User user)
    {
        if (await _userRepo.ExistsByEmail(user.Email))
            throw new InvalidOperationException("Email is already registered.");

        if (await _userRepo.ExistsByUserName(user.UserName))
            throw new InvalidOperationException("Username is already taken.");

        // bcrypt hash via PasswordHasher<User> (per document)
        user.PasswordHash = _passwordHasher.HashPassword(user, user.PasswordHash);
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        var created = await _userRepo.AddUser(user);
        _logger.LogInformation("New user registered: {UserName} ({Email})",
            created.UserName, created.Email);

        // Publish AFTER DB save — UserId is now valid
        await _rabbitMqPublisher.PublishUserRegisteredAsync(new UserRegisteredEvent
        {
            UserId       = created.UserId,
            UserName     = created.UserName,
            DisplayName  = created.DisplayName,
            Email        = created.Email,
            Role         = created.Role ?? "User",
            RegisteredAt = created.CreatedAt
        });

        return created;
    }

    /// <summary>
    /// Login — returns JWT access token + refresh token on success
    /// ChatHub uses Context.UserIdentifier (mapped to UserId sub claim)
    /// </summary>
    // public async Task<AuthResponseDto?> Login(string email, string password)
    // {
    //     var user = await _userRepo.FindByEmail(email);

    //     //Case 1: user not found
    //     if (user is null)
    //         return null;

    //     //Case 2: deactivated user (IMPORTANT)
    //     if (!user.IsActive)
    //         throw new UnauthorizedAccessException("ACCOUNT_DEACTIVATED");

    //     var result = _passwordHasher.VerifyHashedPassword(
    //         user, user.PasswordHash, password);

    //     //Case 3: wrong password
    //     if (result == PasswordVerificationResult.Failed)
    //         return null;

    //     //login success
    //     await _userRepo.UpdateOnlineStatus(user.UserId, true, DateTime.UtcNow);

    //     user.IsOnline = true;
    //     user.LastSeen = DateTime.UtcNow;

    //     var accessToken = _jwtService.GenerateAccessToken(user);
    //     var refreshTokenString = _jwtService.GenerateRefreshToken();
    //     var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

    //     var refreshToken = new RefreshToken
    //     {
    //         Token = refreshTokenString,
    //         UserId = user.UserId,
    //         ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry),
    //         CreatedAt = DateTime.UtcNow
    //     };

    //     await _refreshTokenRepo.AddToken(refreshToken);

    //     return new AuthResponseDto
    //     {
    //         AccessToken = accessToken,
    //         RefreshToken = refreshTokenString,
    //         ExpiresAt = DateTime.UtcNow.AddMinutes(
    //             int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
    //         User = MapToProfileDto(user)
    //     };
    // }
    // public async Task<AuthResponseDto?> Login(string email, string password)
    // {
    //     var user = await _userRepo.FindByEmail(email);

    //     if (user is null)
    //         return null;

    //     if (!user.IsActive)
    //         throw new UnauthorizedAccessException("ACCOUNT_DEACTIVATED");

    //     var result = _passwordHasher.VerifyHashedPassword(
    //         user, user.PasswordHash, password);

    //     if (result == PasswordVerificationResult.Failed)
    //         return null;

    //     // Clear deactivation/reactivation cache — fresh login issues a new token
    //     // that will be issued AFTER any deactivation timestamp, but cleaning up
    //     // ensures no stale cache entries affect the new session.
    //     _tokenBlacklist.ClearUserBlacklist(user.UserId);

    //     await _userRepo.UpdateOnlineStatus(user.UserId, true, DateTime.UtcNow);
    //     user.IsOnline = true;
    //     user.LastSeen = DateTime.UtcNow;

    //     var accessToken = _jwtService.GenerateAccessToken(user);
    //     var refreshTokenString = _jwtService.GenerateRefreshToken();
    //     var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

    //     var refreshToken = new RefreshToken
    //     {
    //         Token = refreshTokenString,
    //         UserId = user.UserId,
    //         ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry),
    //         CreatedAt = DateTime.UtcNow
    //     };

    //     await _refreshTokenRepo.AddToken(refreshToken);

    //     return new AuthResponseDto
    //     {
    //         AccessToken = accessToken,
    //         RefreshToken = refreshTokenString,
    //         ExpiresAt = DateTime.UtcNow.AddMinutes(
    //             int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
    //         User = MapToProfileDto(user)
    //     };
    // }
    public async Task<AuthResponseDto?> Login(string email, string password)
    {
        var user = await _userRepo.FindByEmail(email);

        if (user is null)
            return null;

        if (!user.IsActive)
            throw new UnauthorizedAccessException("ACCOUNT_DEACTIVATED");

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
            return null;

        // Clear cache entries
        // _tokenBlacklist.ClearUserBlacklist(user.UserId);

        // Reset LoggedOutAt and set online in one single update
        // to avoid EF tracking conflict
        var trackedUser = await _userRepo.FindByUserId(user.UserId);
        if (trackedUser != null)
        {
            trackedUser.LoggedOutAt = null;
            trackedUser.IsOnline = true;
            trackedUser.LastSeen = DateTime.UtcNow;
            await _userRepo.UpdateUser(trackedUser);
        }

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshTokenString = _jwtService.GenerateRefreshToken();
        var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.AddToken(refreshToken);

        // Publish online presence event AFTER tokens saved
        await _rabbitMqPublisher.PublishUserOnlineAsync(new UserOnlineEvent
        {
            UserId   = user.UserId,
            LastSeen = DateTime.UtcNow
        });

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
            User = MapToProfileDto(user)
        };
    }
    public async Task<AuthResponseDto> GenerateTokensForUser(User user)
    {
        //Generate JWT
        var accessToken = _jwtService.GenerateAccessToken(user);

        // Generate Refresh Token
        var refreshToken = _jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7")
            ),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.AddToken(refreshTokenEntity);

        //Update online status
        await _userRepo.UpdateOnlineStatus(user.UserId, true, DateTime.UtcNow);

        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;

        // Publish online presence event (OAuth login path)
        await _rabbitMqPublisher.PublishUserOnlineAsync(new UserOnlineEvent
        {
            UserId   = user.UserId,
            LastSeen = DateTime.UtcNow
        });

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")
            ),
            User = MapToProfileDto(user)
        };
    }

    public async Task<bool> ChangeUserRole(int userId, string newRole)
    {
        var user = await _userRepo.FindByUserId(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!user.IsActive)
            throw new InvalidOperationException("Cannot change role of deactivated user.");

        if (newRole != "User" && newRole != "Admin")
            throw new InvalidOperationException("Invalid role. Must be 'User' or 'Admin'.");

        user.Role = newRole;
        await _userRepo.UpdateUser(user);

        _logger.LogInformation(
            "Role changed for UserId={UserId} to Role={Role}", userId, newRole);

        // Publish AFTER DB save — consumers can update role-based caches
        await _rabbitMqPublisher.PublishUserRoleChangedAsync(new UserRoleChangedEvent
        {
            UserId    = userId,
            NewRole   = newRole,
            ChangedAt = DateTime.UtcNow
        });

        return true;
    }

    public async Task<IList<User>> GetUsersByRole(string role)
    {
        if (role != "User" && role != "Admin")
            throw new InvalidOperationException("Invalid role. Must be 'User' or 'Admin'.");

        var users = await _userRepo.FindAllByRole(role);
        return users;
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _userRepo.FindByEmail(email.ToLower().Trim());
    }

    /// <summary>
    /// Logout — revokes all refresh tokens + sets IsOnline = false
    /// </summary>
    public async Task Logout(string refreshToken, int currentUserId, string? accessToken = null)
    {
        var tokenEntity = await _refreshTokenRepo.FindByToken(refreshToken);

        if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.IsExpired)
            throw new UnauthorizedAccessException("Invalid refresh token");

        if (tokenEntity.UserId != currentUserId)
            throw new UnauthorizedAccessException("This token does not belong to you");

        await _refreshTokenRepo.RevokeToken(refreshToken);
        await _userRepo.UpdateOnlineStatus(currentUserId, false, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (handler.CanReadToken(accessToken))
                {
                    var jwt = handler.ReadJwtToken(accessToken);

                    var jti = jwt.Claims
                        .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                    if (!string.IsNullOrEmpty(jti))
                    {
                        // Cache — fast check
                        _tokenBlacklist.BlacklistToken(jti, jwt.ValidTo);

                        // DB — survives server restart
                        var user = await _userRepo.FindByUserId(currentUserId);
                        if (user != null)
                        {
                            // user.LoggedOutAt = DateTime.UtcNow; // ← LoggedOutAt not DeactivatedAt
                            await _userRepo.UpdateUser(user);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to blacklist token during logout");
            }
        }

        _logger.LogInformation("User logged out: UserId={UserId}", currentUserId);

        // Publish offline presence event AFTER DB update
        await _rabbitMqPublisher.PublishUserOfflineAsync(new UserOfflineEvent
        {
            UserId   = currentUserId,
            LastSeen = DateTime.UtcNow
        });
    }
    // public async Task<bool> ValidateToken(string token)
    // {
    //     if (!_jwtService.ValidateToken(token))
    //         return false;

    //     var userId = _jwtService.GetUserIdFromToken(token);
    //     if (userId == null)
    //         return false;

    //     // Get user from DB — contains DeactivatedAt and WasReactivated
    //     var user = await _userRepo.FindByUserId(userId.Value);
    //     if (user == null || !user.IsActive)
    //         return false;

    //     var handler = new JwtSecurityTokenHandler();
    //     var jwt = handler.ReadJwtToken(token);

    //     // JTI blacklist check
    //     var jti = jwt.Claims
    //         .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

    //     if (!string.IsNullOrEmpty(jti) && _tokenBlacklist.IsBlacklisted(jti))
    //         return false;

    //     // Get token issued-at time
    //     var iatClaim = jwt.Claims
    //         .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat ||
    //                              c.Type == "iat")?.Value;

    //     if (long.TryParse(iatClaim, out long iatUnix))
    //     {
    //         var tokenIssuedAt = DateTimeOffset
    //             .FromUnixTimeSeconds(iatUnix).UtcDateTime;

    //         // Check against DB — survives server restarts
    //         if (user.DeactivatedAt.HasValue && tokenIssuedAt <= user.DeactivatedAt.Value)
    //         {
    //             _logger.LogInformation(
    //                 "VALIDATE: token blocked — issued before deactivation. " +
    //                 "tokenIssuedAt={IssuedAt}, deactivatedAt={DeactivatedAt}",
    //                 tokenIssuedAt, user.DeactivatedAt.Value);
    //             return false;
    //         }
    //     }

    //     return true;
    // }

    public async Task<bool> ValidateToken(string token)
    {
        if (!_jwtService.ValidateToken(token))
            return false;

        var userId = _jwtService.GetUserIdFromToken(token);
        if (userId == null)
            return false;

        var user = await _userRepo.FindByUserId(userId.Value);
        if (user == null)
            return false;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // JTI blacklist check
        var jti = jwt.Claims
            .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.IsNullOrEmpty(jti) && _tokenBlacklist.IsBlacklisted(jti))
            return false;

        var iatClaim = jwt.Claims
            .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat ||
                                 c.Type == "iat")?.Value;

        if (long.TryParse(iatClaim, out long iatUnix))
        {
            var tokenIssuedAt = DateTimeOffset
                .FromUnixTimeSeconds(iatUnix).UtcDateTime;

            // Check deactivation timestamp
            // if (user.DeactivatedAt.HasValue && tokenIssuedAt <= user.DeactivatedAt.Value)
            //     return false;
            // Check logout timestamp — old token issued before logout is invalid
            if (_tokenBlacklist.IsUserBlacklisted(userId.Value, tokenIssuedAt))
                return false;
        }
        return true;
    }

    /// <summary>
    /// RefreshToken — rotates refresh token (revoke old, issue new)
    /// </summary>
    public async Task<AuthResponseDto?> RefreshToken(string refreshToken, int currentUserId)
    {
        var stored = await _refreshTokenRepo.FindByToken(refreshToken);
        if (stored is null || !stored.IsActive)
            return null;

        if (stored.UserId != currentUserId)
            return null;

        var user = stored.User;
        if (!user.IsActive) return null;

        // Revoke old token
        await _refreshTokenRepo.RevokeToken(refreshToken);

        // Issue new tokens
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var refreshExpiry = int.Parse(_config["Jwt:RefreshExpiryDays"] ?? "7");

        await _refreshTokenRepo.AddToken(new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiry)
        });

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
            User = MapToProfileDto(user)
        };
    }

    public async Task<User?> GetByEmail(string email)
    {
        email = email.ToLower().Trim();
        return await _userRepo.FindByEmail(email);
    }

    public async Task<User?> GetUserById(int userId)
    {
        var user = await _userRepo.FindByUserId(userId);

        // if (user == null || !user.IsActive)
        //     return null;

        return user;
    }

    public async Task<User?> GetUserByUserName(string userName) =>
        await _userRepo.FindByUserName(userName);

    public async Task<User> UpdateProfile(int userId, User updatedUser)
    {
        var user = await _userRepo.FindByUserId(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!string.IsNullOrWhiteSpace(updatedUser.DisplayName))
            user.DisplayName = updatedUser.DisplayName;
        if (!string.IsNullOrWhiteSpace(updatedUser.Bio))
            user.Bio = updatedUser.Bio;
        if (!string.IsNullOrWhiteSpace(updatedUser.AvatarUrl))
            user.AvatarUrl = updatedUser.AvatarUrl;

        var updated = await _userRepo.UpdateUser(user);

        // Publish AFTER DB save — UC2/UC3 can refresh cached display name/avatar
        await _rabbitMqPublisher.PublishUserProfileUpdatedAsync(new UserProfileUpdatedEvent
        {
            UserId      = updated.UserId,
            UserName    = updated.UserName,
            DisplayName = updated.DisplayName,
            AvatarUrl   = updated.AvatarUrl,
            Bio         = updated.Bio,
            UpdatedAt   = DateTime.UtcNow
        });

        return updated;
    }

    /// <summary>
    /// ChangePassword — hashes new password, revokes all refresh tokens,
    /// sets IsOnline = false (user must re-login), and blacklists the current
    /// access token so it cannot be used again before its natural expiry.
    /// </summary>
    public async Task ChangePassword(
        int userId,
        string currentPassword,
        string newPassword,
        string? currentAccessToken = null)
    {
        var user = await _userRepo.FindByUserId(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, currentPassword);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _userRepo.UpdateUser(user);

        // Revoke all refresh tokens on password change for security
        await _refreshTokenRepo.RevokeAllUserTokens(userId);

        // FIX 1: Set IsOnline = false — user must login again with new password
        await _userRepo.UpdateOnlineStatus(userId, false, DateTime.UtcNow);

        // FIX 2: Blacklist the current access token so it cannot be reused before expiry.
        // Without this, the old JWT stays valid until it naturally expires, which means
        // a caller can keep using it to hit any protected endpoint even after password change.
        if (!string.IsNullOrWhiteSpace(currentAccessToken))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(currentAccessToken))
                {
                    var jwt = handler.ReadJwtToken(currentAccessToken);
                    var jti = jwt.Claims
                        .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)
                        ?.Value;

                    if (!string.IsNullOrEmpty(jti))
                        _tokenBlacklist.BlacklistToken(jti, jwt.ValidTo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not blacklist access token for UserId={UserId}", userId);
            }
        }

        _logger.LogInformation(
            "Password changed for UserId={UserId}. User set offline and access token invalidated.",
            userId);

        // Publish security events AFTER all DB/token operations complete
        await _rabbitMqPublisher.PublishUserPasswordChangedAsync(new UserPasswordChangedEvent
        {
            UserId    = userId,
            ChangedAt = DateTime.UtcNow
        });

        // Password change forces offline — notify presence consumers
        await _rabbitMqPublisher.PublishUserOfflineAsync(new UserOfflineEvent
        {
            UserId   = userId,
            LastSeen = DateTime.UtcNow
        });
    }

    /// <summary>
    /// SearchUsers — full-text search on UserName and DisplayName
    /// per document: UserController.SearchUsers()
    /// </summary>
    public async Task<IList<User>> SearchUsers(string query)
    {
        var users = await _userRepo.SearchUsers(query);
        return users;
    }
    public async Task<bool> ReactivateUser(int userId)
    {
        var user = await _userRepo.FindByUserId(userId);

        if (user == null)
            return false;

        if (user.IsActive)
            throw new InvalidOperationException("User is already active and does not need reactivation.");

        user.IsActive = true;
        user.WasReactivated = true; // ← SAVE TO DB

        await _userRepo.SaveChangesAsync();

        // Still mark in cache for fast checks
        _tokenBlacklist.MarkUserReactivated(userId);

        _logger.LogInformation("Account reactivated: UserId={UserId}", userId);

        // Publish AFTER DB save — UC3 can re-allow user to join rooms
        await _rabbitMqPublisher.PublishUserReactivatedAsync(new UserReactivatedEvent
        {
            UserId        = userId,
            ReactivatedAt = DateTime.UtcNow
        });

        return true;
    }

    /// <summary>
    /// SetOnlineStatus — called from ChatHub lifecycle events
    /// OnConnectedAsync and OnDisconnectedAsync
    /// </summary>
    public async Task SetOnlineStatus(int userId, bool isOnline) =>
        await _userRepo.UpdateOnlineStatus(userId, isOnline, DateTime.UtcNow);

    public async Task<IList<User>> GetAllActiveUsers() =>
        await _userRepo.FindAllActive();

    /// <summary>
    /// DeactivateAccount — IsActive = false prevents login
    /// without deleting message history (per document requirement)
    /// </summary>
    // public async Task DeactivateAccount(int userId, string? currentAccessToken)
    // {
    //     var user = await _userRepo.FindByUserId(userId)
    //         ?? throw new KeyNotFoundException($"User {userId} not found.");

    //     //ADD THIS BLOCK (IMPORTANT)
    //     if (!user.IsActive)
    //         throw new InvalidOperationException("User is already deactivated");

    //     // STEP 1 — Set offline
    //     user.IsOnline = false;
    //     user.LastSeen = DateTime.UtcNow;

    //     // STEP 2 — Deactivate
    //     user.IsActive = false;

    //     await _userRepo.UpdateUser(user);

    //     // STEP 3 — Revoke refresh tokens
    //     await _refreshTokenRepo.RevokeAllUserTokens(userId);

    //     // STEP 4 — Blacklist token (existing code)
    //     if (!string.IsNullOrWhiteSpace(currentAccessToken))
    //     {
    //         try
    //         {
    //             var handler = new JwtSecurityTokenHandler();

    //             if (handler.CanReadToken(currentAccessToken))
    //             {
    //                 var jwt = handler.ReadJwtToken(currentAccessToken);

    //                 var jti = jwt.Claims
    //                     .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)
    //                     ?.Value;

    //                 if (!string.IsNullOrEmpty(jti))
    //                 {
    //                     _tokenBlacklist.BlacklistToken(jti, jwt.ValidTo);
    //                 }
    //             }
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogWarning(ex,
    //                 "Failed to blacklist token during deactivation for UserId={UserId}", userId);
    //         }
    //     }

    //     _logger.LogWarning("Account deactivated: UserId={UserId}", userId);
    // }
    public async Task DeactivateAccount(int userId, string? currentAccessToken)
    {
        var user = await _userRepo.FindByUserId(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!user.IsActive)
            throw new InvalidOperationException("User is already deactivated");

        user.IsOnline = false;
        user.LastSeen = DateTime.UtcNow;
        user.IsActive = false;
        user.DeactivatedAt = DateTime.UtcNow; // ← SAVE TO DB
        user.WasReactivated = false;

        await _userRepo.UpdateUser(user);
        await _refreshTokenRepo.RevokeAllUserTokens(userId);

        // Still blacklist in cache for fast checks
        _tokenBlacklist.BlacklistUser(userId, DateTime.UtcNow);

        // Publish to RabbitMQ AFTER DB save — UC2 and UC3 consume this event
        await _rabbitMqPublisher.PublishUserDeactivatedAsync(new UserDeactivatedEvent
        {
            UserId = userId,
            DeactivatedAt = user.DeactivatedAt ?? DateTime.UtcNow
        });

        // Also publish offline presence — user is now offline
        await _rabbitMqPublisher.PublishUserOfflineAsync(new UserOfflineEvent
        {
            UserId   = userId,
            LastSeen = user.LastSeen
        });

        _logger.LogWarning("Account deactivated: UserId={UserId}", userId);
    }
    // ── Mapping helper ──────────────────────────────────────────
    private static UserProfileDto MapToProfileDto(User user) => new()
    {
        UserId = user.UserId,
        UserName = user.UserName,
        DisplayName = user.DisplayName,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        Bio = user.Bio,
        IsOnline = user.IsOnline,
        LastSeen = user.LastSeen,
        CreatedAt = user.CreatedAt,
        IsActive = user.IsActive
    };
}
