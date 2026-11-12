namespace Workspace.Repositories.Entities;

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
