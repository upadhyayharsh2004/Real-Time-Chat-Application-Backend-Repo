using ConnectHub.Auth.Models.Entities;

namespace ConnectHub.Auth.Services.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int? GetUserIdFromToken(string token);
    bool ValidateToken(string token);
}
