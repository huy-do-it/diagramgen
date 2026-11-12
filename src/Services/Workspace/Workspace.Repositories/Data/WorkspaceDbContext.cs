using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Outbox;
using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Data;

public class WorkspaceDbContext : DbContext, IOutboxDbContext
{
    public WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<UserDirectory> UserDirectory => Set<UserDirectory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Project
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("Projects");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(100);
            b.Property(p => p.Description).HasMaxLength(500);
            b.Property(p => p.OwnerId).IsRequired();
            b.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(p => p.CreatedAt).IsRequired();
            b.Property(p => p.RowVersion).IsRowVersion();

            // Soft-delete global query filter
            b.HasQueryFilter(p => !p.IsDeleted);

            b.HasMany(p => p.Members)
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(p => p.Invitations)
                .WithOne(i => i.Project)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProjectMember (Composite PK)
        modelBuilder.Entity<ProjectMember>(b =>
        {
            b.ToTable("ProjectMembers");
            b.HasKey(m => new { m.ProjectId, m.UserId });
            b.Property(m => m.Role).IsRequired().HasMaxLength(20);
            b.Property(m => m.JoinedAt).IsRequired();
            b.HasIndex(m => m.UserId);
        });

        // Invitation
        modelBuilder.Entity<Invitation>(b =>
        {
            b.ToTable("Invitations");
            b.HasKey(i => i.Id);
            b.Property(i => i.Status).IsRequired().HasMaxLength(20);
            b.Property(i => i.CreatedAt).IsRequired();
            b.Property(i => i.ExpiresAt).IsRequired();
            b.HasIndex(i => new { i.ProjectId, i.InviteeId, i.Status });
            b.HasIndex(i => new { i.InviteeId, i.Status });
        });

        // UserDirectory
        modelBuilder.Entity<UserDirectory>(b =>
        {
            b.ToTable("UserDirectory");
            b.HasKey(u => u.UserId);
            b.Property(u => u.Username).IsRequired().HasMaxLength(50);
            b.HasIndex(u => u.Username).IsUnique();
            b.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            b.Property(u => u.Role).IsRequired().HasMaxLength(20);
            b.Property(u => u.IsLocked).IsRequired();
        });

        // BuildingBlocks Outbox & Inbox tables
        modelBuilder.ApplyOutboxInbox();
    }
}
