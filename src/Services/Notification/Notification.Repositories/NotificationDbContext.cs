using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Outbox;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;
using UMLForge.Notification.Repositories.Entities;

namespace UMLForge.Notification.Repositories;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<ProjectRecipient> ProjectRecipients => Set<ProjectRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyOutboxInbox();
        var notification = modelBuilder.Entity<NotificationEntity>();
        notification.HasKey(x => x.Id);
        notification.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        notification.Property(x => x.Type).HasMaxLength(80).IsRequired();
        notification.Property(x => x.Title).HasMaxLength(200).IsRequired();
        notification.Property(x => x.Link).HasMaxLength(500).IsRequired();
        notification.Property(x => x.CreatedAt).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        notification.Property(x => x.ReadAt).HasConversion(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : (DateTime?)null);

        var recipient = modelBuilder.Entity<ProjectRecipient>();
        recipient.HasKey(x => new { x.ProjectId, x.UserId });
        recipient.Property(x => x.Role).HasMaxLength(16).IsRequired();
        recipient.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
    }
}
