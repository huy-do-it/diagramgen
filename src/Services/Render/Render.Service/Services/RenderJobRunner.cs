using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Repositories.Repositories;
using UMLForge.Render.Service.Interfaces;

namespace UMLForge.Render.Service.Services;

public sealed class RenderJobRunner(
    IRenderJobRepository repository, IRenderPipeline pipeline, IOutbox outbox,
    TimeProvider clock, ILogger<RenderJobRunner> logger) : IRenderJobRunner
{
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken = default)
    {
        var job = await repository.GetNextQueuedAsync(cancellationToken);
        if (job is null) return false;
        using var activity = CorrelationContext.Begin(job.CorrelationId == Guid.Empty ? job.Id : job.CorrelationId, "render.job");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationKeys.LogProperty] = CorrelationContext.Current!.Value, ["JobId"] = job.Id
        });

        job.Status = RenderJobStatus.Running;
        job.Attempt++;
        job.StartedAt = clock.GetUtcNow().UtcDateTime;
        job.FinishedAt = null;
        job.Error = null;
        job.DurationMs = null;
        outbox.Add(StreamNames.Render, new RenderStarted(job.Id, job.DiagramId, job.ProjectId, job.DiagramVersion));
        try
        {
            // Status + Attempt là concurrency tokens: chỉ một Worker thắng; Started cũng rollback nếu thua.
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Another worker claimed render job {JobId}.", job.Id);
            return false;
        }

        // Không giữ SQL transaction qua network. Shutdown/lỗi hạ tầng để lại Running bền vững cho M-04.
        Models.RenderResult result;
        try
        {
            result = await pipeline.ProcessJobAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Render job {JobId} was interrupted with {ErrorType}; its durable state is retained for recovery.", job.Id, ex.GetType().Name);
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        job.FinishedAt = clock.GetUtcNow().UtcDateTime;
        job.DurationMs = result.DurationMs;
        if (result.IsSuccess)
        {
            job.Artifact ??= new RenderArtifact { JobId = job.Id };
            job.Artifact.PlantUml = result.PlantUml!;
            job.Artifact.SvgXml = result.SvgXml!;
            job.Status = RenderJobStatus.Done;
            outbox.Add(StreamNames.Render, new RenderCompleted(job.Id, job.DiagramId, job.ProjectId, job.DiagramVersion,
                job.Title, result.SvgXml!, result.PlantUml!, result.DurationMs, job.RequestedBy));
        }
        else
        {
            job.Status = RenderJobStatus.Failed;
            var error = result.Error!;
            job.Error = error[..Math.Min(error.Length, 1000)];
            outbox.Add(StreamNames.Render, new RenderFailed(job.Id, job.DiagramId, job.ProjectId, job.DiagramVersion,
                job.Title, job.Error, job.RequestedBy));
        }

        // Một SaveChanges: trạng thái + artifact + Outbox; stale owner/SQL lỗi không được commit từng phần.
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not commit result for render job {JobId}, attempt {Attempt}: {ErrorType}.", job.Id, job.Attempt, ex.GetType().Name);
            throw;
        }
        logger.LogInformation("Render job {JobId} finished as {Status} in {DurationMs} ms.", job.Id, job.Status, job.DurationMs);
        return true;
    }
}
