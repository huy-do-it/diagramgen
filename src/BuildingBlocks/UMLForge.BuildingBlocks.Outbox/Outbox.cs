using Microsoft.EntityFrameworkCore;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Outbox;

internal sealed record OutboxSettings<TDbContext>(string Producer)
    where TDbContext : DbContext, IOutboxDbContext;

internal sealed class Outbox<TDbContext> : IOutbox
    where TDbContext : DbContext, IOutboxDbContext
{
    private readonly TDbContext _dbContext;
    private readonly OutboxSettings<TDbContext> _settings;

    public Outbox(TDbContext dbContext, OutboxSettings<TDbContext> settings)
    {
        _dbContext = dbContext;
        _settings = settings;
    }

    public void Add<T>(string stream, T payload) where T : class
    {
        var type = EventTypeRegistry.NameOf<T>();
        var expectedStream = EventTypeRegistry.StreamOf(type);
        if (!string.Equals(stream, expectedStream, StringComparison.Ordinal))
            throw new ArgumentException($"Sự kiện '{type}' thuộc stream '{expectedStream}', không phải '{stream}'.", nameof(stream));

        // Giữ CorrelationId của request / sự kiện đang xử lý để lần theo được cả chuỗi
        var envelope = EventEnvelope.Create(_settings.Producer, payload, CorrelationContext.Current);

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            Stream = stream,
            Type = type,
            Envelope = EventJson.Serialize(envelope),
            CreatedAt = envelope.OccurredAt,
        });
    }
}
