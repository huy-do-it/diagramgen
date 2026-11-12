namespace UMLForge.Diagram.Service.Events;

public sealed record RenderRequested(
    Guid DiagramId,
    Guid ProjectId,
    int Version,
    string DiagramType,
    string Title,
    string ModelJson,
    Guid ActorUserId
);
