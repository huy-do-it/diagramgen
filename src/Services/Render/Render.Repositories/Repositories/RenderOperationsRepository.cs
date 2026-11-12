using Microsoft.EntityFrameworkCore;
using UMLForge.Render.Repositories.Entities;
namespace UMLForge.Render.Repositories.Repositories;

public sealed class RenderOperationsRepository(RenderDbContext db)
{
    public Task<List<RenderJob>> StaleAsync(DateTime cutoff, CancellationToken ct) => db.RenderJobs
        .Where(x => x.Status == RenderJobStatus.Running && x.StartedAt < cutoff).OrderBy(x => x.StartedAt).Take(100).ToListAsync(ct);
    public Task<RenderJob?> FindAsync(Guid id, CancellationToken ct) => db.RenderJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<int> SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<(List<RenderJob> Items, int Total)> JobsAsync(RenderJobStatus? status, Guid? diagramId, DateTime? from, DateTime? to, int page, int size, CancellationToken ct)
    {
        var q = db.RenderJobs.AsNoTracking().Where(x => (!status.HasValue || x.Status == status) && (!diagramId.HasValue || x.DiagramId == diagramId)
            && (!from.HasValue || x.CreatedAt >= from) && (!to.HasValue || x.CreatedAt < to));
        var count = await q.CountAsync(ct);
        return (await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct), count);
    }
    public async Task<(List<RenderDailyReport> Items, int Total)> ReportsAsync(DateOnly? from, DateOnly? to, int page, int size, CancellationToken ct)
    {
        var q = db.RenderDailyReports.AsNoTracking().Where(x => (!from.HasValue || x.Date >= from) && (!to.HasValue || x.Date < to));
        var count = await q.CountAsync(ct);
        return (await q.OrderByDescending(x => x.Date).Skip((page-1)*size).Take(size).ToListAsync(ct), count);
    }
    public async Task<string> CleanupAsync(DateTime failedBefore, DateTime processedBefore, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Giữ dữ liệu của cohort chưa chốt để cleanup không làm thiếu báo cáo.
        var jobs = await db.RenderJobs.Where(x => x.Status == RenderJobStatus.Failed && x.FinishedAt < failedBefore
            && db.RenderDailyReports.Any(r => r.Date == DateOnly.FromDateTime(x.CreatedAt))).ExecuteDeleteAsync(ct);
        var artifacts = await db.RenderArtifacts.Where(x => !db.RenderJobs.Any(j => j.Id == x.JobId)).ExecuteDeleteAsync(ct);
        var inbox = await db.InboxMessages.Where(x => x.ProcessedAt < processedBefore).ExecuteDeleteAsync(ct);
        var outbox = await db.OutboxMessages.Where(x => x.PublishedAt != null && x.PublishedAt < processedBefore).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        return $"removed {jobs} failed jobs, {artifacts} orphan artifacts, {inbox} inbox, {outbox} published outbox";
    }
    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginReportAsync(CancellationToken ct)
        => db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
    public Task<bool> ReportExistsLockedAsync(DateOnly date, CancellationToken ct)
        => db.RenderDailyReports.FromSqlInterpolated($"SELECT * FROM [RenderDailyReports] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] = {date}").AnyAsync(ct);
    public async Task<List<DateOnly>> UnreportedDaysAsync(DateOnly before, CancellationToken ct)
    {
        var end = before.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await db.RenderJobs.AsNoTracking().Where(x => x.CreatedAt < end)
            .Select(x => DateOnly.FromDateTime(x.CreatedAt)).Distinct()
            .Where(day => !db.RenderDailyReports.Any(r => r.Date == day)).OrderBy(day => day).Take(100).ToListAsync(ct);
    }
    public async Task<(int Total, int Active, int Done, int Failed, double AvgMs)> ReportDataAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        var q = db.RenderJobs.Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        return (await q.CountAsync(ct), await q.CountAsync(x => x.Status == RenderJobStatus.Queued || x.Status == RenderJobStatus.Running, ct),
            await q.CountAsync(x => x.Status == RenderJobStatus.Done, ct), await q.CountAsync(x => x.Status == RenderJobStatus.Failed, ct),
            await q.Where(x => (x.Status == RenderJobStatus.Done || x.Status == RenderJobStatus.Failed) && x.DurationMs != null).AverageAsync(x => (double?)x.DurationMs, ct) ?? 0);
    }
    public void AddReport(RenderDailyReport report) => db.RenderDailyReports.Add(report);
}
