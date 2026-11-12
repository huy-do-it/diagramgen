using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Web;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Repositories.Repositories;
using UMLForge.Render.Service.Models;
namespace UMLForge.Render.Service.Services;
public sealed class RenderOperationsService(RenderOperationsRepository repository, IStreamMaintenance streams, IOutbox outbox, TimeProvider clock, IValidator<RenderQuery> validator)
{
    public async Task<string> ReconcileAsync(CancellationToken ct)
    {
        Exception? brokerFailure = null;
        var pendingCount = 0;
        try
        {
            var pending = await streams.GetPendingAsync(StreamNames.Diagram, ConsumerGroups.RenderSvc, TimeSpan.FromSeconds(60), ct);
            foreach (var p in pending.Where(x => x.DeliveryCount >= 5)) await streams.MoveToDeadLetterAsync(StreamNames.Diagram, ConsumerGroups.RenderSvc, p.Id, ct);
            await streams.ClaimAsync(StreamNames.Diagram, ConsumerGroups.RenderSvc, EventConsumerIdentity.Name, pending.Where(x => x.DeliveryCount < 5 && x.Consumer != EventConsumerIdentity.Name).Select(x => x.Id), ct);
            pendingCount = pending.Count;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { brokerFailure = ex; }
        var now = clock.GetUtcNow().UtcDateTime; var count = 0;
        foreach (var job in await repository.StaleAsync(now.AddMinutes(-5), ct))
        {
            using var activity = CorrelationContext.Begin(job.CorrelationId == Guid.Empty ? job.Id : job.CorrelationId, "render.reconcile");
            job.Status = RenderJobStatus.Failed; job.FinishedAt = now; job.Error = "Render bị gián đoạn quá 5 phút.";
            outbox.Add(StreamNames.Render, new RenderFailed(job.Id, job.DiagramId, job.ProjectId, job.DiagramVersion, job.Title, job.Error, job.RequestedBy));
            try { await repository.SaveAsync(ct); count++; }
            catch (DbUpdateConcurrencyException) { break; } // Scope kết thúc, bỏ tracker/Outbox của writer thua.
        }
        if (brokerFailure is not null) throw new InvalidOperationException($"SQL reconciliation completed ({count} failed); Redis maintenance failed ({brokerFailure.GetType().Name}).");
        return $"examined {pendingCount} pending, failed {count} stale running jobs";
    }
    public Task<string> CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return repository.CleanupAsync(now.AddDays(-7), now.AddDays(-3), ct);
    }
    public async Task<string> ReportAsync(CancellationToken ct)
    {
        var yesterday = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-1);
        var days = await repository.UnreportedDaysAsync(yesterday, ct);
        days.Add(yesterday);
        var created = 0; var deferred = 0;
        foreach (var day in days)
        {
            await using var tx = await repository.BeginReportAsync(ct);
            if (await repository.ReportExistsLockedAsync(day, ct)) continue;
            var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var data = await repository.ReportDataAsync(start, start.AddDays(1), ct);
            // Chỉ chốt cohort đã terminal; các ngày trì hoãn được tìm lại ở lượt sau.
            if (data.Active > 0) { deferred++; continue; }
            repository.AddReport(new RenderDailyReport { Date = day, Total = data.Total, Done = data.Done,
                Failed = data.Failed, AvgMs = data.AvgMs, SuccessRate = data.Total == 0 ? 0 : 100.0 * data.Done / data.Total });
            await repository.SaveAsync(ct);
            await tx.CommitAsync(ct);
            created++;
        }
        return $"created {created} daily reports, deferred {deferred} days with active jobs";
    }
    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy job.");
        if (job.Status != RenderJobStatus.Failed) throw new BusinessRuleException("RETRY_NOT_FAILED", "Chỉ retry job Failed.");
        job.Status = RenderJobStatus.Queued; job.StartedAt = null; job.FinishedAt = null; job.DurationMs = null; job.Error = null;
        try { await repository.SaveAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new BusinessRuleException("JOB_CONFLICT", "Job đã thay đổi; hãy tải lại."); }
    }
    private async Task ValidateAsync(RenderQuery q, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(q, ct);
        if (!result.IsValid) throw new BusinessRuleException("VALIDATION_ERROR", "Tham số truy vấn không hợp lệ.", 400)
        { Errors = result.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()) };
    }
    public async Task<PagedResult<RenderJobResponse>> JobsAsync(RenderQuery q, CancellationToken ct)
    {
        await ValidateAsync(q, ct);
        var result = await repository.JobsAsync(q.Status is null ? null : Enum.Parse<RenderJobStatus>(q.Status), q.DiagramId, q.From, q.To, q.Page, q.PageSize, ct);
        return PagedResult<RenderJobResponse>.Create(result.Items.Select(x => new RenderJobResponse(x.Id, x.DiagramId, x.ProjectId, x.DiagramVersion, x.Status.ToString(), x.Attempt, x.Error, x.CreatedAt, x.StartedAt, x.FinishedAt, x.DurationMs)).ToList(),q.Page,q.PageSize,result.Total);
    }
    public async Task<PagedResult<RenderDailyReport>> ReportsAsync(RenderQuery q, CancellationToken ct)
    {
        await ValidateAsync(q,ct);
        var r = await repository.ReportsAsync(q.From.HasValue ? DateOnly.FromDateTime(q.From.Value) : null,q.To.HasValue ? DateOnly.FromDateTime(q.To.Value) : null,q.Page,q.PageSize,ct);
        return PagedResult<RenderDailyReport>.Create(r.Items,q.Page,q.PageSize,r.Total);
    }
}
