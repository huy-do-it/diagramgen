namespace UMLForge.Diagram.Service.Events;

public sealed record CommentAdded(
    Guid CommentId,
    Guid DiagramId,
    Guid ProjectId,
    Guid AuthorId,
    string Content
);
