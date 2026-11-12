using Microsoft.EntityFrameworkCore;
using UMLForge.Notification.Repositories.Entities;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;
namespace UMLForge.Notification.Repositories.Repositories;
public sealed class NotificationEventRepository(NotificationDbContext db) : INotificationEventRepository
{
    public Task<List<ProjectRecipient>> RecipientsAsync(Guid projectId, CancellationToken ct)
        => db.ProjectRecipients.Where(x => x.ProjectId == projectId).ToListAsync(ct);
    public void AddRecipient(ProjectRecipient recipient) => db.ProjectRecipients.Add(recipient);
    public void RemoveRecipients(IEnumerable<ProjectRecipient> recipients) => db.ProjectRecipients.RemoveRange(recipients);
    public void AddNotification(NotificationEntity notification) => db.Notifications.Add(notification);
    public Task<int> PurgeReadAsync(DateTime readBefore, CancellationToken ct)
        => db.Notifications.Where(x => x.IsRead && x.ReadAt != null && x.ReadAt < readBefore).ExecuteDeleteAsync(ct);
}
