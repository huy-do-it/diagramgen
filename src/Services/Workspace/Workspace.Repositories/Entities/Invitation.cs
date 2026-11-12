namespace Workspace.Repositories.Entities;

public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid InviteeId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Declined, Expired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
