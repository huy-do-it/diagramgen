using UMLForge.BuildingBlocks.Jobs;
using UMLForge.Notification.Service.Services;
namespace UMLForge.Notification.API.Jobs;
public sealed class PurgeReadNotificationsJob(IServiceScopeFactory scopes, ILogger<PurgeReadNotificationsJob> logger) : ScheduledJobBase(scopes, logger)
{
    public override string JobName => "purge-read-notifications";
    protected override TimeSpan Interval => TimeSpan.FromHours(6);
    protected override async Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct)
    {
        var count = await scoped.GetRequiredService<NotificationMaintenanceService>().PurgeReadAsync(ct);
        return $"removed {count} notifications read more than 30 days ago";
    }
}
