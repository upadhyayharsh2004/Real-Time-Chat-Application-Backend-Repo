namespace ConnectHub.Auth.Services.Interfaces;

public interface ITokenBlacklistService
{
    // Blacklist a specific token by its JTI (logout, password change, self-deactivation)
    void BlacklistToken(string jti, DateTime tokenExpiry);
    bool IsBlacklisted(string jti);

    // Blacklist ALL tokens for a user by timestamp (admin deactivation)
    void BlacklistUser(int userId, DateTime deactivatedAt);
    bool IsUserBlacklisted(int userId, DateTime tokenIssuedAt);

    // Mark user as reactivated — old tokens still blocked but message changes
    void MarkUserReactivated(int userId);
    bool IsUserReactivated(int userId, DateTime tokenIssuedAt);

    // Called on successful login — clears all user-level blocks
    void ClearUserBlacklist(int userId);
}