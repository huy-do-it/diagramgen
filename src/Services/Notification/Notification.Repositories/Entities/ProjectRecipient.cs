namespace UMLForge.Notification.Repositories.Entities;

public sealed class ProjectRecipient
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
}
