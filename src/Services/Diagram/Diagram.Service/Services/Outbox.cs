using System.Diagnostics;
using System.Text.Json;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.Events;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Service.Services;

public class Outbox : IOutbox
{
    private readonly IOutboxRepository _outboxRepository;

    public Outbox(IOutboxRepository outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public void Add<T>(string stream, T payload) where T : class
    {
        var eventId = Guid.NewGuid();
        var typeName = typeof(T).Name;
        var occurredAt = DateTime.UtcNow;

        var correlationId = Guid.NewGuid();
        if (Activity.Current != null && Guid.TryParse(Activity.Current.Id, out var parsedGuid))
        {
            correlationId = parsedGuid;
        }

        var envelope = new EventEnvelope<T>(
            EventId: eventId,
            Type: typeName,
            Version: 1,
            OccurredAt: occurredAt,
            CorrelationId: correlationId,
            Producer: "diagram",
            Payload: payload
        );

        var envelopeJson = JsonSerializer.Serialize(envelope);

        var message = new OutboxMessage
        {
            Id = eventId,
            Stream = stream,
            Type = typeName,
            Envelope = envelopeJson,
            CreatedAt = occurredAt,
            PublishedAt = null,
            Attempts = 0
        };

        _outboxRepository.AddMessage(message);
    }
}
