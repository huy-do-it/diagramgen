using UMLForge.Notification.Repositories.Entities;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;
namespace UMLForge.Notification.Repositories.Repositories;
public interface INotificationEventRepository
{
    Task<List<ProjectRecipient>> RecipientsAsync(Guid projectId, CancellationToken ct);
    void AddRecipient(ProjectRecipient recipient);
    void RemoveRecipients(IEnumerable<ProjectRecipient> recipients);
    void AddNotification(NotificationEntity notification);
    Task<int> PurgeReadAsync(DateTime readBefore, CancellationToken ct);
}
