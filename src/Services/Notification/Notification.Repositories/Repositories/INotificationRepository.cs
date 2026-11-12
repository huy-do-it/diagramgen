using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;

namespace UMLForge.Notification.Repositories.Repositories;

public interface INotificationRepository
{
    Task<(IReadOnlyList<NotificationEntity> Items, int TotalItems)> GetPageAsync(
        Guid userId, int page, int pageSize, bool unreadOnly, string? type, string? search, bool ascending,
        CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetOwnerIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> MarkReadAsync(Guid id, Guid userId, DateTime readAt, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default);
    void AddRange(IEnumerable<NotificationEntity> notifications);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
