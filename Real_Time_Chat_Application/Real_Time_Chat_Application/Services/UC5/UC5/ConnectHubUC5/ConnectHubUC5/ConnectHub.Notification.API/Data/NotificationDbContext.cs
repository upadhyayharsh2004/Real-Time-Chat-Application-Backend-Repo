using ConnectHub.Notification.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConnectHub.Notification.Data;
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

    public DbSet<Models.Entities.Notification> Notifications =>
        Set<Models.Entities.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Models.Entities.Notification>(entity =>
        {
            entity.HasKey(n => n.NotificationId);

            // Most common query: unread count + list for a user
            entity.HasIndex(n => new { n.RecipientId, n.IsRead })
                  .HasDatabaseName("IX_Notifications_RecipientId_IsRead");

            // Paginated list ordered by time
            entity.HasIndex(n => new { n.RecipientId, n.SentAt })
                  .HasDatabaseName("IX_Notifications_RecipientId_SentAt");

            // Find notification by related entity (e.g. MessageId)
            entity.HasIndex(n => new { n.RelatedId, n.RelatedType })
                  .HasDatabaseName("IX_Notifications_RelatedId_RelatedType");

            // Find by type
            entity.HasIndex(n => n.Type)
                  .HasDatabaseName("IX_Notifications_Type");

            entity.Property(n => n.Type).HasMaxLength(30).IsRequired();
            entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.RelatedType).HasMaxLength(50);
            entity.Property(n => n.IsRead).HasDefaultValue(false);
            entity.Property(n => n.SentAt).HasDefaultValueSql("NOW()");
        });
    }
}
