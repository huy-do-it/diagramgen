namespace UMLForge.Notification.Service.Models;

public sealed record NotificationResponse(Guid Id, string Type, string Title, string Link, bool IsRead, DateTime? ReadAt, DateTime CreatedAt);
public sealed record NotificationPage(IReadOnlyList<NotificationResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record UnreadCountResponse(int Count);
public sealed record MarkReadResponse(bool IsRead);
public sealed record MarkAllReadResponse(int Updated);
public sealed record SeedNotificationsResponse(int Created);
public enum MarkReadOutcome { Success, NotFound, Forbidden }
