using System.Text.Json;
using Identity.Repositories.Entities;
using UMLForge.Contracts.Events;

namespace Identity.Repositories.Data;

public class Outbox : IOutbox
{
    private readonly IdentityDbContext _context;

    public Outbox(IdentityDbContext context)
    {
        _context = context;
    }

    public void Add<T>(string stream, T payload) where T : class
    {
        var envelope = EventEnvelope.Create("identity", payload);
        var json = JsonSerializer.Serialize(envelope);

        var outboxMessage = new OutboxMessage
        {
            Id = envelope.EventId,
            Stream = stream,
            Type = envelope.Type,
            Envelope = json,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = null,
            Attempts = 0,
            LastError = null
        };

        _context.OutboxMessages.Add(outboxMessage);
    }
}
