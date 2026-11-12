using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Context;

public class DiagramDbContext : DbContext, IOutboxDbContext
{
    public DiagramDbContext(DbContextOptions<DiagramDbContext> options) : base(options)
    {
    }

    public DbSet<Entities.Diagram> Diagrams => Set<Entities.Diagram>();
    public DbSet<DiagramVersion> DiagramVersions => Set<DiagramVersion>();
    public DbSet<MemberAccess> MemberAccesses => Set<MemberAccess>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Áp dụng bảng OutboxMessages + InboxMessages chuẩn của BuildingBlocks.Outbox
        modelBuilder.ApplyOutboxInbox();

        modelBuilder.Entity<Entities.Diagram>(b =>
        {
            b.ToTable("Diagrams");
            b.HasKey(d => d.Id);
            b.Property(d => d.Title).HasMaxLength(150).IsRequired();
            b.Property(d => d.ModelJson).IsRequired();
            b.Property(d => d.Type).IsRequired();
            b.Property(d => d.Status).IsRequired();
            b.Property(d => d.RowVersion).IsRowVersion();
            b.HasQueryFilter(d => !d.IsDeleted);

            b.HasIndex(d => d.ProjectId);
            b.HasIndex(d => new { d.ProjectId, d.IsDeleted });
        });

        modelBuilder.Entity<DiagramVersion>(b =>
        {
            b.ToTable("DiagramVersions");
            b.HasKey(v => v.Id);
            b.Property(v => v.Title).HasMaxLength(150).IsRequired();
            b.Property(v => v.ModelJson).IsRequired();

            b.HasIndex(v => new { v.DiagramId, v.Version }).IsUnique();
            b.HasIndex(v => v.DiagramId);

            b.HasOne(v => v.Diagram)
             .WithMany(d => d.Versions)
             .HasForeignKey(v => v.DiagramId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberAccess>(b =>
        {
            b.ToTable("MemberAccess");
            b.HasKey(m => new { m.ProjectId, m.UserId });
            b.Property(m => m.Role).HasMaxLength(50).IsRequired();
            b.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<Review>(b =>
        {
            b.ToTable("Reviews");
            b.HasKey(r => r.Id);
            b.Property(r => r.Status).IsRequired();
            b.Property(r => r.SnapshotJson).IsRequired();
            b.Property(r => r.Note).HasMaxLength(2000);
            b.HasIndex(r => r.ProjectId);
            b.HasIndex(r => new { r.ProjectId, r.SubmittedAt });
        });

        modelBuilder.Entity<Comment>(b =>
        {
            b.ToTable("Comments");
            b.HasKey(c => c.Id);
            b.Property(c => c.Content).HasMaxLength(2000).IsRequired();
            b.HasIndex(c => c.DiagramId);
            b.HasIndex(c => new { c.DiagramId, c.CreatedAt });

            b.HasOne(c => c.Diagram)
             .WithMany()
             .HasForeignKey(c => c.DiagramId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
