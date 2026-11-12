namespace UMLForge.Contracts.Events;

/// <summary>Phong bì chung của mọi sự kiện trên Redis Streams (TASKS §1.4).</summary>
public sealed record EventEnvelope<T>(
    Guid EventId,
    string Type,
    int Version,
    DateTime OccurredAt,
    Guid CorrelationId,
    string Producer,
    T Payload);

public static class EventEnvelope
{
    public const int CurrentVersion = 1;

    /// <summary>Tạo envelope mới cho payload; dùng trong Outbox, test và công cụ seed.</summary>
    public static EventEnvelope<T> Create<T>(string producer, T payload, Guid? correlationId = null)
        where T : class
        => new(
            Guid.NewGuid(),
            EventTypeRegistry.NameOf<T>(),
            CurrentVersion,
            DateTime.UtcNow,
            correlationId ?? Guid.NewGuid(),
            producer,
            payload);
}
