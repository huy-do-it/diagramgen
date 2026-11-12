namespace UMLForge.Diagram.Service.Events;

public sealed record CommentAdded(
    Guid CommentId,
    Guid DiagramId,
    Guid ProjectId,
    string DiagramTitle,
    Guid ActorUserId,
    string? Content = null
)
{
    public Guid AuthorId => ActorUserId;
}
