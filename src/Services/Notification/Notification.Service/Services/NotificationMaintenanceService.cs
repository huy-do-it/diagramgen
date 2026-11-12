using UMLForge.Notification.Repositories.Repositories;

namespace UMLForge.Notification.Service.Services;

public sealed class NotificationMaintenanceService(INotificationEventRepository repository, TimeProvider clock)
{
    public Task<int> PurgeReadAsync(CancellationToken cancellationToken = default)
    {
        var readBefore = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        return repository.PurgeReadAsync(readBefore, cancellationToken);
    }
}
