namespace UMLForge.Diagram.Service.Events;

public sealed record ReviewDecided(
    Guid ReviewId,
    Guid ProjectId,
    string Status,
    Guid DecidedBy,
    string? Note
);
