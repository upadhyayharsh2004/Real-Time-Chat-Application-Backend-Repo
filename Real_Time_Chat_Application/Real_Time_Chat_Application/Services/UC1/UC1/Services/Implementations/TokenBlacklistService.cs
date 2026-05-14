using ConnectHub.Auth.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ConnectHub.Auth.Services.Implementations;

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;

    public TokenBlacklistService(IMemoryCache cache)
    {
        _cache = cache;
    }

    // ── JTI blacklist (specific token) ───────────────────────────

    public void BlacklistToken(string jti, DateTime tokenExpiry)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;
        if (tokenExpiry <= DateTime.UtcNow) return;

        _cache.Set(
            key: $"blacklist:{jti}",
            value: true,
            absoluteExpiration: tokenExpiry
        );
    }

    public bool IsBlacklisted(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;
        return _cache.TryGetValue($"blacklist:{jti}", out _);
    }

    // ── User-level deactivation block (by timestamp) ─────────────
    // Stores the UTC moment of deactivation.
    // Any token issued at or before this moment is blocked.

    public void BlacklistUser(int userId, DateTime deactivatedAt)
    {
        _cache.Set(
            key: $"user_deactivated:{userId}",
            value: deactivatedAt,
            absoluteExpiration: DateTimeOffset.UtcNow.AddDays(30)
        );
    }

    public bool IsUserBlacklisted(int userId, DateTime tokenIssuedAt)
    {
        if (_cache.TryGetValue($"user_deactivated:{userId}", out DateTime deactivatedAt))
            return tokenIssuedAt <= deactivatedAt;

        return false;
    }

    // ── Reactivation marker ──────────────────────────────────────
    // When admin reactivates, we store a reactivation timestamp.
    // Old tokens (issued before deactivation) are still blocked,
    // but now return "Account reactivated. Please login." instead
    // of "Account deactivated."

    public void MarkUserReactivated(int userId)
    {
        _cache.Set(
            key: $"user_reactivated:{userId}",
            value: DateTime.UtcNow,
            absoluteExpiration: DateTimeOffset.UtcNow.AddDays(30)
        );
    }

    public bool IsUserReactivated(int userId, DateTime tokenIssuedAt)
    {
        // Only returns true if:
        // 1. A reactivation marker exists
        // 2. AND the token was still issued before the deactivation (i.e. it's an old token)
        if (_cache.TryGetValue($"user_reactivated:{userId}", out DateTime _))
        {
            if (_cache.TryGetValue($"user_deactivated:{userId}", out DateTime deactivatedAt))
                return tokenIssuedAt <= deactivatedAt;
        }
        return false;
    }

    // ── Clear all user-level cache (called on successful login) ──
    // After fresh login, new token is issued AFTER deactivation timestamp,
    // but we clean up anyway to keep cache tidy.

    public void ClearUserBlacklist(int userId)
    {
        _cache.Remove($"user_deactivated:{userId}");
        _cache.Remove($"user_reactivated:{userId}");
    }
}