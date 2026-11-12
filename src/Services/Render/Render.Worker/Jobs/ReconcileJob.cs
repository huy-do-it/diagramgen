using UMLForge.BuildingBlocks.Jobs;
using UMLForge.Render.Service.Services;
namespace UMLForge.Render.Worker.Jobs;
public sealed class ReconcileJob(IServiceScopeFactory scopes, ILogger<ReconcileJob> logger) : ScheduledJobBase(scopes, logger)
{
    public override string JobName => "reconcile";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(2);
    protected override bool RunOnStartup => false;
    protected override Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct) => scoped.GetRequiredService<RenderOperationsService>().ReconcileAsync(ct);
}
