using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class InboxRepository : GenericRepository<InboxMessage>, IInboxRepository
{
    public InboxRepository(DiagramDbContext context) : base(context)
    {
    }

    public async Task<bool> HasBeenProcessedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(i => i.EventId == eventId && i.Consumer == consumer, cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(i => i.EventId == eventId && i.Consumer == consumer, cancellationToken);
        if (existing == null)
        {
            await _dbSet.AddAsync(new InboxMessage
            {
                EventId = eventId,
                Consumer = consumer,
                ProcessedAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }
}
