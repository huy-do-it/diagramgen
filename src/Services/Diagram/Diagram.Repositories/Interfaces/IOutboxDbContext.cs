using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
}
