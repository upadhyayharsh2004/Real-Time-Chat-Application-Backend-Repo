using ConnectHub.ChatRoom.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.ChatRoom.Data;

/// <summary>
/// ChatRoomDbContext — EF Core DbContext for ConnectHub ChatRoom service.
/// SQL Server via Microsoft.EntityFrameworkCore.SqlServer.
/// Indexes optimized for common queries:
///   (CreatedByUserId) — user's rooms
///   (RoomType, IsActive) — browse public rooms
///   (RoomId, UserId) — member lookup
///   (RoomId, Role) — role check
/// </summary>
public class ChatRoomDbContext : DbContext
{
    public ChatRoomDbContext(DbContextOptions<ChatRoomDbContext> options) : base(options) { }

    public DbSet<Models.Entities.ChatRoom> ChatRooms => Set<Models.Entities.ChatRoom>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<RoomInvite> RoomInvites => Set<RoomInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chatroom");

        base.OnModelCreating(modelBuilder);

        // ── ChatRoom ─────────────────────────────────────────────────
        modelBuilder.Entity<Models.Entities.ChatRoom>(entity =>
        {
            entity.HasKey(r => r.RoomId);

            // Index for browsing public rooms — GET /api/rooms/public
            entity.HasIndex(r => new { r.RoomType, r.IsActive })
                  .HasDatabaseName("IX_ChatRooms_RoomType_IsActive");

            // Index for user's created rooms
            entity.HasIndex(r => r.CreatedByUserId)
                  .HasDatabaseName("IX_ChatRooms_CreatedByUserId");

            // Index for soft-deleted rooms
            entity.HasIndex(r => r.DeletedAt)
                  .HasDatabaseName("IX_ChatRooms_DeletedAt");

            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
            entity.Property(r => r.RoomType).HasMaxLength(10).HasDefaultValue("PUBLIC");
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.Property(r => r.CreatedAt).HasDefaultValueSql("NOW()"); 

            entity.HasMany(r => r.Members)
                  .WithOne(m => m.Room)
                  .HasForeignKey(m => m.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(r => r.Invites)
                  .WithOne(i => i.Room)
                  .HasForeignKey(i => i.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RoomMember ────────────────────────────────────────────────
        modelBuilder.Entity<RoomMember>(entity =>
        {
            entity.HasKey(m => m.RoomMemberId);

            // Unique: one membership record per user per room
            entity.HasIndex(m => new { m.RoomId, m.UserId })
                  .IsUnique()
                  .HasDatabaseName("IX_RoomMembers_RoomId_UserId");

            // Index for role-based queries (admin check)
            entity.HasIndex(m => new { m.RoomId, m.Role })
                  .HasDatabaseName("IX_RoomMembers_RoomId_Role");

            // Index for user's joined rooms
            entity.HasIndex(m => new { m.UserId, m.IsActive })
                  .HasDatabaseName("IX_RoomMembers_UserId_IsActive");

            entity.Property(m => m.Role).HasMaxLength(20).HasDefaultValue("MEMBER");
            entity.Property(m => m.IsActive).HasDefaultValue(true);
            entity.Property(m => m.JoinedAt).HasDefaultValueSql("NOW()");
        });

        // ── RoomInvite ────────────────────────────────────────────────
        modelBuilder.Entity<RoomInvite>(entity =>
        {
            entity.HasKey(i => i.InviteId);

            entity.HasIndex(i => new { i.RoomId, i.InvitedUserId, i.Status })
                  .HasDatabaseName("IX_RoomInvites_RoomId_UserId_Status");

            entity.HasIndex(i => new { i.InvitedUserId, i.Status })
                  .HasDatabaseName("IX_RoomInvites_InvitedUserId_Status");

            entity.Property(i => i.Status).HasMaxLength(20).HasDefaultValue("PENDING");
            entity.Property(i => i.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
