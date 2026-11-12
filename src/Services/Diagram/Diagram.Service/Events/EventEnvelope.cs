namespace UMLForge.Diagram.Service.Events;

public sealed record EventEnvelope<T>(
    Guid EventId,
    string Type,
    int Version,
    DateTime OccurredAt,
    Guid CorrelationId,
    string Producer,
    T Payload
);
