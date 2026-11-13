using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Repositories.Entities;

public class Diagram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public DiagramType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModelJson { get; set; } = "{}";
    public DiagramStatus Status { get; set; } = DiagramStatus.Draft;
    public int CurrentVersion { get; set; } = 1;
    public string? SvgXml { get; set; }
    public string? PlantUml { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? LastError { get; set; }
    public bool IsDeleted { get; set; } = false;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation property
    public ICollection<DiagramVersion> Versions { get; set; } = new List<DiagramVersion>();
}
