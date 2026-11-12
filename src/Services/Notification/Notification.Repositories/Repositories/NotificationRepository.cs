using Microsoft.EntityFrameworkCore;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;

namespace UMLForge.Notification.Repositories.Repositories;

public sealed class NotificationRepository(NotificationDbContext context) : INotificationRepository
{
    public async Task<(IReadOnlyList<NotificationEntity> Items, int TotalItems)> GetPageAsync(
        Guid userId, int page, int pageSize, bool unreadOnly, string? type, string? search, bool ascending,
        CancellationToken cancellationToken = default)
    {
        var query = context.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search));
        var count = await query.CountAsync(cancellationToken);
        var ordered = ascending ? query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        var items = await ordered.Skip(checked((page - 1) * pageSize)).Take(pageSize).ToListAsync(cancellationToken);
        return (items, count);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

    public Task<Guid?> GetOwnerIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Notifications.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(cancellationToken);

    public Task<int> MarkReadAsync(Guid id, Guid userId, DateTime readAt, CancellationToken cancellationToken = default)
        => context.Notifications.Where(x => x.Id == id && x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, readAt), cancellationToken);

    public Task<int> MarkAllReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default)
        => context.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, readAt), cancellationToken);

    public void AddRange(IEnumerable<NotificationEntity> notifications) => context.Notifications.AddRange(notifications);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}
