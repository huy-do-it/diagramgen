using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Repositories.Entities;

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Submitted;
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Note { get; set; }
    public string SnapshotJson { get; set; } = "[]";
}
