namespace UMLForge.Contracts.Events;

// Stream: diagram.events — producer: diagram
// DiagramType: 1 BusinessContext · 2 UseCase · 3 Sequence · 4 Activity · 5 State (giống gRPC).
public sealed record RenderRequested(
    Guid DiagramId, Guid ProjectId, int Version, int DiagramType,
    string Title, string ModelJson, Guid ActorUserId);

public sealed record ReviewSubmitted(Guid ReviewId, Guid ProjectId, Guid ActorUserId);

// Decision: giá trị trong ReviewDecisions (Approved / ChangesRequested).
public sealed record ReviewDecided(
    Guid ReviewId, Guid ProjectId, string Decision, string? Note, Guid ActorUserId);

public sealed record CommentAdded(
    Guid CommentId, Guid DiagramId, Guid ProjectId, string DiagramTitle, Guid ActorUserId);
