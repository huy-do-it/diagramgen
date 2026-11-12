namespace UMLForge.Render.Repositories.Entities;

public enum RenderJobStatus { Queued, Running, Done, Failed }

public sealed class RenderJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiagramId { get; set; }
    public Guid ProjectId { get; set; }
    public int DiagramVersion { get; set; }
    public int DiagramType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModelJson { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public RenderJobStatus Status { get; set; } = RenderJobStatus.Queued;
    public int Attempt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public RenderArtifact? Artifact { get; set; }
}
