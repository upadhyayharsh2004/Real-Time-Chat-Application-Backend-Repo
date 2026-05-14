using ConnectHub.Auth.Models.Entities;

namespace ConnectHub.Auth.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByToken(string token);
    Task<IList<RefreshToken>> FindActiveByUserId(int userId);
    Task AddToken(RefreshToken token);
    Task RevokeToken(string token);
    Task RevokeAllUserTokens(int userId);
    Task SaveChangesAsync();
}
