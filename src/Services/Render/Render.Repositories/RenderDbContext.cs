using Microsoft.EntityFrameworkCore;
using UMLForge.Render.Repositories.Entities;

namespace UMLForge.Render.Repositories;

public sealed class RenderDbContext(DbContextOptions<RenderDbContext> options) : DbContext(options)
{
    public DbSet<RenderJob> RenderJobs => Set<RenderJob>();
    public DbSet<RenderArtifact> RenderArtifacts => Set<RenderArtifact>();
    public DbSet<RenderDailyReport> RenderDailyReports => Set<RenderDailyReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<RenderJob>();
        job.HasKey(x => x.Id);
        job.HasIndex(x => new { x.DiagramId, x.DiagramVersion }).IsUnique();
        job.HasIndex(x => new { x.Status, x.CreatedAt });
        job.Property(x => x.Title).HasMaxLength(200).IsRequired();
        job.Property(x => x.ModelJson).IsRequired();
        job.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        job.Property(x => x.Error).HasMaxLength(1000);
        // SQL datetime2 không giữ DateTime.Kind; khôi phục UTC khi đọc để API sau này xuất đúng múi giờ.
        job.Property(x => x.CreatedAt).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        job.Property(x => x.StartedAt).HasConversion(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : (DateTime?)null);
        job.Property(x => x.FinishedAt).HasConversion(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : (DateTime?)null);
        job.ToTable("RenderJobs", table =>
        {
            table.HasCheckConstraint("CK_RenderJobs_Version", "[DiagramVersion] > 0");
            table.HasCheckConstraint("CK_RenderJobs_Type", "[DiagramType] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_RenderJobs_Attempt", "[Attempt] >= 0");
        });

        var artifact = modelBuilder.Entity<RenderArtifact>();
        artifact.HasKey(x => x.JobId);
        artifact.Property(x => x.PlantUml).IsRequired();
        artifact.Property(x => x.SvgXml).IsRequired();
        job.HasOne(x => x.Artifact).WithOne().HasForeignKey<RenderArtifact>(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        var report = modelBuilder.Entity<RenderDailyReport>();
        report.HasKey(x => x.Date);
    }
}
