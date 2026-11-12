using UMLForge.BuildingBlocks.Jobs;
using UMLForge.Render.Service.Services;
namespace UMLForge.Render.Worker.Jobs;
public sealed class CleanupJob(IServiceScopeFactory scopes, ILogger<CleanupJob> logger) : ScheduledJobBase(scopes, logger)
{
    public override string JobName => "cleanup";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(60);
    protected override bool RunOnStartup => false;
    protected override Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct) => scoped.GetRequiredService<RenderOperationsService>().CleanupAsync(ct);
}
