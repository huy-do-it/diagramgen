namespace UMLForge.Contracts.Events;

// Stream: render.events — producer: render
public sealed record RenderStarted(Guid JobId, Guid DiagramId, Guid ProjectId, int Version);

public sealed record RenderCompleted(
    Guid JobId, Guid DiagramId, Guid ProjectId, int Version, string Title,
    string Svg, string PlantUml, long DurationMs, Guid RequestedBy);

public sealed record RenderFailed(
    Guid JobId, Guid DiagramId, Guid ProjectId, int Version, string Title,
    string Error, Guid RequestedBy);
