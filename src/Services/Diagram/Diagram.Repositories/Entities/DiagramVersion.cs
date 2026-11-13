namespace UMLForge.Diagram.Repositories.Entities;

public class DiagramVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiagramId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModelJson { get; set; } = "{}";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Diagram? Diagram { get; set; }
}
