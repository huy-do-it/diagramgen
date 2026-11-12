using UMLForge.Notification.Service.Models;

namespace UMLForge.Notification.Service.Interfaces;

public interface INotificationService
{
    Task<NotificationPage> GetPageAsync(Guid userId, NotificationQuery query, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MarkReadOutcome> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> SeedDevelopmentAsync(Guid userId, CancellationToken cancellationToken = default);
}
