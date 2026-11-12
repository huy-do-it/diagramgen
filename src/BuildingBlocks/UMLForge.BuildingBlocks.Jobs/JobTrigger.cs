namespace UMLForge.BuildingBlocks.Jobs;

/// <summary>Chạy tay job định kỳ theo tên (dùng cho API admin và lúc demo).</summary>
public interface IJobTrigger
{
    IReadOnlyList<JobInfo> Jobs { get; }

    /// <summary>Trả null nếu không có job tên này.</summary>
    Task<JobRunResult?> RunNowAsync(string jobName, CancellationToken ct = default);
}

internal sealed class JobTrigger : IJobTrigger
{
    private readonly IReadOnlyList<ScheduledJobBase> _jobs;

    public JobTrigger(IEnumerable<ScheduledJobBase> jobs)
    {
        _jobs = jobs.ToList();
    }

    public IReadOnlyList<JobInfo> Jobs
        => _jobs.Select(job => new JobInfo(job.JobName, job.ScheduleInterval, job.LastResult)).ToList();

    public async Task<JobRunResult?> RunNowAsync(string jobName, CancellationToken ct = default)
    {
        var job = _jobs.FirstOrDefault(j => j.JobName.Equals(jobName, StringComparison.OrdinalIgnoreCase));
        return job is null ? null : await job.RunNowAsync("manual", ct);
    }
}
