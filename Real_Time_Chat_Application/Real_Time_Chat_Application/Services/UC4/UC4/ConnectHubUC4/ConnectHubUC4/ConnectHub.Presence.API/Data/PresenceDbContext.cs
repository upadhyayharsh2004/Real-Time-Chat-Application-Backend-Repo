using ConnectHub.Presence.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Presence.Data;

/// <summary>
/// PresenceDbContext — EF Core DbContext for Presence service.
/// Persists LastSeen and IsOnline to SQL Server so presence state
/// survives service restarts (in-memory ConcurrentDictionary is lost on restart).
/// </summary>
public class PresenceDbContext : DbContext
{
    public PresenceDbContext(DbContextOptions<PresenceDbContext> options) : base(options) { }

    public DbSet<UserPresence> UserPresences => Set<UserPresence>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("presence");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserPresence>(entity =>
        {
            entity.HasKey(p => p.UserId);
            entity.HasIndex(p => p.IsOnline).HasDatabaseName("IX_UserPresences_IsOnline");
            entity.HasIndex(p => p.LastSeen).HasDatabaseName("IX_UserPresences_LastSeen");
            entity.Property(p => p.IsOnline).HasDefaultValue(false);
            entity.Property(p => p.ActiveConnectionCount).HasDefaultValue(0);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(p => p.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<UserConnection>(entity =>
        {
            entity.HasKey(c => c.ConnectionId);
            entity.Property(c => c.ConnectionId).HasMaxLength(200);
            entity.HasIndex(c => c.UserId).HasDatabaseName("IX_UserConnections_UserId");
            entity.Property(c => c.ConnectedAt).HasDefaultValueSql("NOW()");
        });
    }
}
