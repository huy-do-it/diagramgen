using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UMLForge.BuildingBlocks.Jobs;

/// <summary>
/// Job định kỳ: BackgroundService + PeriodicTimer. Mỗi lần chạy tạo scope mới, bắt mọi exception
/// (job không bao giờ chết), log "[JobName] tóm tắt in N ms". Chạy tay được qua <see cref="IJobTrigger"/>.
/// </summary>
public abstract class ScheduledJobBase : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    // Không cho lần chạy tay và lần chạy theo lịch chồng lên nhau
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected ScheduledJobBase(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Tên kebab-case, dùng trong URL chạy tay, ví dụ "expire-invitations".</summary>
    public abstract string JobName { get; }

    protected abstract TimeSpan Interval { get; }

    protected virtual bool RunOnStartup => false;

    public JobRunResult? LastResult { get; private set; }

    internal TimeSpan ScheduleInterval => Interval;

    /// <summary>Làm việc của job; trả về câu tóm tắt để log, ví dụ "expired 3 invitations".</summary>
    protected abstract Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct);

    public async Task<JobRunResult> RunNowAsync(string trigger, CancellationToken ct)
    {
        await _runLock.WaitAsync(ct);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var summary = await RunOnceAsync(scope.ServiceProvider, ct);
            stopwatch.Stop();
            _logger.LogInformation("[{JobName}] {Summary} in {ElapsedMs} ms ({Trigger})",
                JobName, summary, stopwatch.ElapsedMilliseconds, trigger);
            LastResult = new JobRunResult(JobName, true, summary, stopwatch.ElapsedMilliseconds, DateTime.UtcNow, trigger);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[{JobName}] failed in {ElapsedMs} ms ({Trigger})",
                JobName, stopwatch.ElapsedMilliseconds, trigger);
            LastResult = new JobRunResult(JobName, false, ex.Message, stopwatch.ElapsedMilliseconds, DateTime.UtcNow, trigger);
        }
        finally
        {
            _runLock.Release();
        }

        return LastResult;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nhường luồng để không chặn quá trình khởi động host
        await Task.Yield();
        try
        {
            if (RunOnStartup)
                await RunNowAsync("startup", stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunNowAsync("schedule", stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host đang tắt
        }
    }

    public override void Dispose()
    {
        _runLock.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
