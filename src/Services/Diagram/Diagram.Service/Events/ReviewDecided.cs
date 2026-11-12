namespace UMLForge.Diagram.Service.Events;

public sealed record ReviewDecided(
    Guid ReviewId,
    Guid ProjectId,
    string Decision,
    Guid ActorUserId,
    string? Note = null
)
{
    public string Status => Decision;
    public Guid DecidedBy => ActorUserId;
}
