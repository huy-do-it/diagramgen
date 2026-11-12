using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IInboxRepository : IGenericRepository<InboxMessage>
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
}
