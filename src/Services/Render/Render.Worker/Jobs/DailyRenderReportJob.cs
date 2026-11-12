using UMLForge.BuildingBlocks.Jobs;
using UMLForge.Render.Service.Services;
namespace UMLForge.Render.Worker.Jobs;
public sealed class DailyRenderReportJob(IServiceScopeFactory scopes, ILogger<DailyRenderReportJob> logger) : ScheduledJobBase(scopes, logger)
{
    public override string JobName => "daily-render-report";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(60);
    protected override bool RunOnStartup => true;
    protected override Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct) => scoped.GetRequiredService<RenderOperationsService>().ReportAsync(ct);
}
