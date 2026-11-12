namespace Workspace.Repositories.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? LecturerId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}
