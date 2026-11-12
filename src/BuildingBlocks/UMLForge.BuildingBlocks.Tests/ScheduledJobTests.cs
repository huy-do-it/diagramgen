using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UMLForge.BuildingBlocks.Jobs;

namespace UMLForge.BuildingBlocks.Tests;

/// <summary>B-02 mục 4: ScheduledJobBase không chết khi lỗi, IJobTrigger chạy tay theo tên.</summary>
public sealed class ScheduledJobTests
{
    private sealed class CountingJob : ScheduledJobBase
    {
        public CountingJob(IServiceScopeFactory scopeFactory, ILogger<CountingJob> logger)
            : base(scopeFactory, logger)
        {
        }

        public int Runs { get; private set; }

        public bool Fail { get; set; }

        public override string JobName => "counting";

        protected override TimeSpan Interval => TimeSpan.FromHours(1);

        protected override Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct)
        {
            Runs++;
            if (Fail)
                throw new InvalidOperationException("lỗi giả lập");
            return Task.FromResult($"run {Runs}");
        }
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScheduledJob<CountingJob>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunNowAsync_JobSucceeds_ReturnsSummary()
    {
        await using var services = BuildServices();
        var trigger = services.GetRequiredService<IJobTrigger>();

        var result = await trigger.RunNowAsync("counting");

        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal("run 1", result.Summary);
        Assert.Equal("manual", result.Trigger);
    }

    [Fact]
    public async Task RunNowAsync_JobThrows_ReturnsFailureAndNextRunStillWorks()
    {
        await using var services = BuildServices();
        var job = services.GetRequiredService<CountingJob>();
        var trigger = services.GetRequiredService<IJobTrigger>();

        job.Fail = true;
        var failed = await trigger.RunNowAsync("counting");
        job.Fail = false;
        var succeeded = await trigger.RunNowAsync("counting");

        Assert.False(failed!.Succeeded);
        Assert.Equal("lỗi giả lập", failed.Summary);
        Assert.True(succeeded!.Succeeded);
        Assert.Equal(2, job.Runs);
    }

    [Fact]
    public async Task RunNowAsync_UnknownJob_ReturnsNull()
    {
        await using var services = BuildServices();

        var result = await services.GetRequiredService<IJobTrigger>().RunNowAsync("not-exists");

        Assert.Null(result);
    }

    [Fact]
    public void AddScheduledJob_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();
        services.AddScheduledJob<CountingJob>();
        services.AddScheduledJob<CountingJob>();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(CountingJob));
    }
}
