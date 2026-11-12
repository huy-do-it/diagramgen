using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Service.Jobs;

public interface IScheduledJob
{
    string JobName { get; }
    Task<string> ExecuteDirectlyAsync(CancellationToken ct = default);
}

public abstract class ScheduledJobBase : BackgroundService, IScheduledJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public abstract string JobName { get; }
    protected abstract TimeSpan Interval { get; }
    protected virtual bool RunOnStartup => false;

    protected ScheduledJobBase(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (RunOnStartup)
        {
            try
            {
                await ExecuteDirectlyAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error executing job {JobName} on startup", JobName);
            }
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExecuteDirectlyAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error executing scheduled job {JobName}", JobName);
            }
        }
    }

    public async Task<string> ExecuteDirectlyAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Executing job {JobName}...", JobName);
        using var scope = _scopeFactory.CreateScope();
        var result = await RunOnceAsync(scope.ServiceProvider, ct);
        _logger.LogInformation("Job {JobName} completed: {Result}", JobName, result);
        return result;
    }

    protected abstract Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct);
}
