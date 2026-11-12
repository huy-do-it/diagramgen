using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Entities;

namespace Identity.Repositories.Data;

public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
}
