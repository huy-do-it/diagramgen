using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class OutboxRepository : GenericRepository<OutboxMessage>, IOutboxRepository
{
    public OutboxRepository(DiagramDbContext context) : base(context)
    {
    }

    public void AddMessage(OutboxMessage message)
    {
        _dbSet.Add(message);
    }

    public async Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<List<OutboxMessage>> GetUnpublishedAsync(int batchSize = 50, CancellationToken cancellationToken = default)
        => GetUnpublishedMessagesAsync(batchSize, cancellationToken);

    public async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (message != null)
        {
            message.PublishedAt = DateTime.UtcNow;
            message.Attempts++;
        }
    }

    public async Task RecordFailureAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (message != null)
        {
            message.Attempts++;
            message.LastError = error;
        }
    }
}
