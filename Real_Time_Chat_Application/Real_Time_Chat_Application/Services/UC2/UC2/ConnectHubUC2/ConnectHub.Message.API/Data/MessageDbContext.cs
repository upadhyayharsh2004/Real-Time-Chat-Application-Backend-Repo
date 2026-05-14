using ConnectHub.Message.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Message.Data;

/// <summary>
/// EF Core DbContext for ConnectHub Message service
/// SQL Server via Microsoft.EntityFrameworkCore.SqlServer
/// Indexes optimised for (SenderId, ReceiverId) and (RoomId, SentAt)
/// as specified in Message-Service Class Diagram
/// </summary>
public class MessageDbContext : DbContext
{
    public MessageDbContext(DbContextOptions<MessageDbContext> options) : base(options) { }

    public DbSet<Models.Entities.Message> Messages => Set<Models.Entities.Message>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<ConversationPin> ConversationPins => Set<ConversationPin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.HasDefaultSchema("message");

        base.OnModelCreating(modelBuilder);

        // ── Message ───────────────────────────────────────────
        modelBuilder.Entity<Models.Entities.Message>(entity =>
        {
            entity.HasKey(m => m.MessageId);

            // Index for direct message queries — (SenderId, ReceiverId)
            entity.HasIndex(m => new { m.SenderId, m.ReceiverId })
                  .HasDatabaseName("IX_Messages_SenderId_ReceiverId");

            // Index for room message queries — (RoomId, SentAt)
            entity.HasIndex(m => new { m.RoomId, m.SentAt })
                  .HasDatabaseName("IX_Messages_RoomId_SentAt");

            // Index for unread queries — IsRead filter
            entity.HasIndex(m => new { m.ReceiverId, m.IsRead })
                  .HasDatabaseName("IX_Messages_ReceiverId_IsRead");

            // Index for soft-delete queries
            entity.HasIndex(m => m.IsDeleted)
                  .HasDatabaseName("IX_Messages_IsDeleted");

            entity.Property(m => m.Content).HasMaxLength(4000).IsRequired();
            entity.Property(m => m.MessageType).HasMaxLength(20).HasDefaultValue("TEXT");
            entity.Property(m => m.SentAt).HasDefaultValueSql("NOW()");
            entity.Property(m => m.IsRead).HasDefaultValue(false);
            entity.Property(m => m.IsDeleted).HasDefaultValue(false);
            entity.Property(m => m.IsEdited).HasDefaultValue(false);

            // Self-referencing FK for reply threading
            entity.HasOne(m => m.ReplyToMessage)
                  .WithMany()
                  .HasForeignKey(m => m.ReplyToMessageId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(m => m.Reactions)
                  .WithOne(r => r.Message)
                  .HasForeignKey(r => r.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MessageReaction ────────────────────────────────────
        modelBuilder.Entity<MessageReaction>(entity =>
        {
            entity.HasKey(r => r.ReactionId);

            // Unique: one emoji per user per message
            entity.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji })
                  .IsUnique()
                  .HasDatabaseName("IX_MessageReactions_MessageId_UserId_Emoji");

            entity.Property(r => r.Emoji).HasMaxLength(10).IsRequired();
            entity.Property(r => r.ReactedAt).HasDefaultValueSql("NOW()");
        });

        // ── ConversationPin ────────────────────────────────────
        modelBuilder.Entity<ConversationPin>(entity =>
        {
            entity.HasKey(p => p.PinId);

            entity.HasIndex(p => new { p.MessageId, p.PinnedByUserId })
                  .IsUnique()
                  .HasDatabaseName("IX_ConversationPins_MessageId_UserId");

            entity.HasOne(p => p.Message)
                  .WithMany()
                  .HasForeignKey(p => p.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.PinnedAt).HasDefaultValueSql("NOW()");
        });
    }
}
