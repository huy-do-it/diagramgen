namespace UMLForge.Notification.Service.Models;

public sealed class NotificationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool UnreadOnly { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "createdAt:desc";
}
