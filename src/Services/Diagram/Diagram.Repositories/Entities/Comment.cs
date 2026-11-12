namespace UMLForge.Diagram.Repositories.Entities;

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiagramId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Diagram? Diagram { get; set; }
}
