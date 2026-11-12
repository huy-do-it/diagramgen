using FluentValidation;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.Interfaces;
using UMLForge.Notification.Service.Models;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;

namespace UMLForge.Notification.Service.Services;

public sealed class NotificationService(INotificationRepository repository, IValidator<NotificationQuery> validator, TimeProvider timeProvider) : INotificationService
{
    public async Task<NotificationPage> GetPageAsync(Guid userId, NotificationQuery query, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, total) = await repository.GetPageAsync(userId, query.Page, query.PageSize, query.UnreadOnly,
            query.Type?.Trim(), query.Search?.Trim(), query.Sort == "createdAt:asc", cancellationToken);
        return new NotificationPage(items.Select(x => new NotificationResponse(x.Id, x.Type, x.Title, x.Link,
            x.IsRead, x.ReadAt, x.CreatedAt)).ToArray(), query.Page, query.PageSize, total, (int)Math.Ceiling((double)total / query.PageSize));
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => repository.CountUnreadAsync(userId, cancellationToken);

    public async Task<MarkReadOutcome> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var owner = await repository.GetOwnerIdAsync(notificationId, cancellationToken);
        if (owner is null) return MarkReadOutcome.NotFound;
        if (owner != userId) return MarkReadOutcome.Forbidden;
        // Update có điều kiện: đọc lặp/đồng thời không làm mới ReadAt, tránh lệch retention M-05.
        await repository.MarkReadAsync(notificationId, userId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return MarkReadOutcome.Success;
    }

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
        => repository.MarkAllReadAsync(userId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public async Task<int> SeedDevelopmentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddRange(Enumerable.Range(1, 5).Select(i => new NotificationEntity
        {
            UserId = userId, Type = "DevelopmentSeed", Title = $"Thông báo mẫu {i}", Link = "/notifications", CreatedAt = now.AddMinutes(-i)
        }));
        await repository.SaveChangesAsync(cancellationToken);
        return 5;
    }
}
