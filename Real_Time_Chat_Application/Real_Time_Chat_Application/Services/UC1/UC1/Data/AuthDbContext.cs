using ConnectHub.Auth.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Auth.Data;

/// <summary>
/// EF Core DbContext for ConnectHub Auth service
/// SQL Server via Microsoft.EntityFrameworkCore.SqlServer
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        base.OnModelCreating(modelBuilder);

        // ── User ──────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.UserId);

            // Unique indexes — prevent duplicate registration
            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasDatabaseName("IX_Users_Email");

            entity.Property(u => u.Email).HasMaxLength(200).IsRequired();

            entity.HasIndex(u => u.UserName)
                  .IsUnique()
                  .HasDatabaseName("IX_Users_UserName");

            entity.Property(u => u.UserName).HasMaxLength(50).IsRequired();

            // Index for online status queries (PresenceService)
            entity.HasIndex(u => u.IsOnline)
                  .HasDatabaseName("IX_Users_IsOnline");

            // Index for active users lookup
            entity.HasIndex(u => u.IsActive)
                  .HasDatabaseName("IX_Users_IsActive");

            entity.Property(u => u.CreatedAt)
              .HasDefaultValueSql("NOW()");

            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();

            entity.Property(u => u.IsActive)
                  .HasDefaultValue(true);
        });

        // ── RefreshToken ───────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.Token)
                  .IsUnique()
                  .HasDatabaseName("IX_RefreshTokens_Token");

            entity.HasIndex(r => r.UserId)
                  .HasDatabaseName("IX_RefreshTokens_UserId");

            entity.Property(r => r.Token).HasMaxLength(450);

            entity.HasOne(r => r.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
