using System.Collections.Concurrent;
using ConnectHub.Presence.Models.DTOs;
using ConnectHub.Presence.Models.Entities;
using ConnectHub.Presence.Models.Events;
using ConnectHub.Presence.Repositories.Interfaces;
using ConnectHub.Presence.Services.Interfaces;

namespace ConnectHub.Presence.Services.Implementations;

/// <summary>
/// PresenceService — implements IPresenceService. Registered as AddSingleton.
///
/// PRIMARY store: ConcurrentDictionary (in-memory, thread-safe, no DB round-trip)
///   _connections: ConcurrentDictionary&lt;int, HashSet&lt;string&gt;&gt;
///                 UserId → Set of active connectionIds (multi-tab/device)
///   _userInfo:    ConcurrentDictionary&lt;string, UserConnection&gt;
///                 ConnectionId → full UserConnection metadata
///
/// SECONDARY store: SQL Server via IPresenceRepository (scoped)
///   Used for LastSeen persistence. Called async (fire-and-forget) so it
///   never blocks the synchronous hot-path ConcurrentDictionary operations.
///
/// Online rule:  _connections[userId].Count > 0
/// Offline rule: all connections for userId removed
/// Multi-device: user stays online while ANY connection remains
///
/// RabbitMQ publish:
///   UserPresenceOnline  — only on FIRST connection (was offline before)
///   UserPresenceOffline — only when LAST connection closes
/// </summary>
public class PresenceService : IPresenceService
{
    // ── In-memory stores (Singleton, thread-safe) ──────────────────
    private readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();
    private readonly ConcurrentDictionary<string, UserConnection> _userInfo   = new();
    private readonly object _lock = new();

    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly IRabbitMqPublisher    _publisher;
    private readonly ILogger<PresenceService> _logger;

    public PresenceService(
        IServiceScopeFactory scopeFactory,
        IRabbitMqPublisher publisher,
        ILogger<PresenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher    = publisher;
        _logger       = logger;
    }

    // ── Class diagram methods ──────────────────────────────────────

    public void UserConnected(int userId, string connectionId, string? deviceInfo = null)
    {
        var conn = new UserConnection
        {
            ConnectionId = connectionId,
            UserId       = userId,
            ConnectedAt  = DateTime.UtcNow,
            DeviceInfo   = deviceInfo
        };

        _userInfo.TryAdd(connectionId, conn);

        bool isFirstConnection;
        lock (_lock)
        {
            if (!_connections.ContainsKey(userId))
                _connections[userId] = new HashSet<string>();

            isFirstConnection = _connections[userId].Count == 0;
            _connections[userId].Add(connectionId);
        }

        var count = _connections[userId].Count;

        _logger.LogInformation(
            "User {UserId} connected. ConnectionId={ConnId}. TotalConnections={Count}",
            userId, connectionId, count);

        // Async DB persist — fire-and-forget, non-blocking
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPresenceRepository>();
                await repo.AddConnection(conn);
                await repo.SetUserOnline(userId, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB persist failed for UserConnected UserId={UserId}", userId);
            }
        });

        // Publish RabbitMQ — ONLY on first connection
        if (isFirstConnection)
        {
            _ = _publisher.PublishUserPresenceOnlineAsync(new UserPresenceOnlineEvent
            {
                UserId      = userId,
                ConnectedAt = DateTime.UtcNow
            });
        }
    }

    public void UserDisconnected(int userId, string connectionId)
    {
        _userInfo.TryRemove(connectionId, out _);

        bool isLastConnection;
        lock (_lock)
        {
            if (_connections.TryGetValue(userId, out var conns))
            {
                conns.Remove(connectionId);
                isLastConnection = conns.Count == 0;
                if (isLastConnection)
                    _connections.TryRemove(userId, out _);
            }
            else
            {
                isLastConnection = true;
            }
        }

        _logger.LogInformation(
            "User {UserId} disconnected. ConnectionId={ConnId}. IsLastConnection={IsLast}",
            userId, connectionId, isLastConnection);

        var lastSeen = DateTime.UtcNow;

        // Async DB persist
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPresenceRepository>();
                await repo.RemoveConnection(connectionId);
                if (isLastConnection)
                    await repo.SetUserOffline(userId, lastSeen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB persist failed for UserDisconnected UserId={UserId}", userId);
            }
        });

        // Publish RabbitMQ — ONLY when last connection closes
        if (isLastConnection)
        {
            _ = _publisher.PublishUserPresenceOfflineAsync(new UserPresenceOfflineEvent
            {
                UserId   = userId,
                LastSeen = lastSeen
            });
        }
    }

    public IList<string> GetConnectionsByUserId(int userId)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(userId, out var conns)
                ? conns.ToList()
                : new List<string>();
        }
    }

    public IList<int> GetOnlineUserIds() =>
        _connections.Keys.ToList();

    public bool IsUserOnline(int userId) =>
        _connections.TryGetValue(userId, out var c) && c.Count > 0;

    public int GetConnectionCount() =>
        _userInfo.Count;

    public IList<UserConnection> GetOnlineUsersInfo() =>
        _userInfo.Values.ToList();

    public void ClearUserConnections(int userId)
    {
        lock (_lock)
        {
            if (_connections.TryRemove(userId, out var connIds))
                foreach (var id in connIds)
                    _userInfo.TryRemove(id, out _);
        }

        _logger.LogInformation("All connections cleared for UserId={UserId}", userId);

        // Async DB cleanup
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPresenceRepository>();
                await repo.RemoveAllConnectionsForUser(userId);
                await repo.SetUserOffline(userId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB persist failed for ClearUserConnections UserId={UserId}", userId);
            }
        });

        // Publish offline event
        _ = _publisher.PublishUserPresenceOfflineAsync(new UserPresenceOfflineEvent
        {
            UserId   = userId,
            LastSeen = DateTime.UtcNow
        });
    }

    // ── Async helpers ──────────────────────────────────────────────

    public Task<IList<UserPresenceDto>> GetPresenceForUsers(IList<int> userIds)
    {
        var result = userIds.Select(id => new UserPresenceDto
        {
            UserId               = id,
            IsOnline             = IsUserOnline(id),
            ActiveConnectionCount = _connections.TryGetValue(id, out var c) ? c.Count : 0
        }).ToList();

        return Task.FromResult<IList<UserPresenceDto>>(result);
    }

    public Task<IList<UserPresenceDto>> GetOnlineUsersDtos()
    {
        var result = _connections.Keys.Select(id => new UserPresenceDto
        {
            UserId               = id,
            IsOnline             = true,
            ActiveConnectionCount = _connections.TryGetValue(id, out var c) ? c.Count : 0
        }).ToList();

        return Task.FromResult<IList<UserPresenceDto>>(result);
    }

    public async Task UpdateLastActiveAt(int userId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPresenceRepository>();
            await repo.UpdateLastActiveAt(userId, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLastActiveAt failed for UserId={UserId}", userId);
        }
    }
}
