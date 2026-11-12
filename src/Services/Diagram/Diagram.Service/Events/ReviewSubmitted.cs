namespace UMLForge.Diagram.Service.Events;

public sealed record ReviewSubmitted(
    Guid ReviewId,
    Guid ProjectId,
    Guid ActorUserId
);
