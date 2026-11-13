using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IOutboxRepository : IGenericRepository<OutboxMessage>
{
    void AddMessage(OutboxMessage message);
    Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(int batchSize = 50, CancellationToken cancellationToken = default);
    Task<List<OutboxMessage>> GetUnpublishedAsync(int batchSize = 50, CancellationToken cancellationToken = default);
    Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default);
    Task RecordFailureAsync(Guid id, string error, CancellationToken cancellationToken = default);
}
