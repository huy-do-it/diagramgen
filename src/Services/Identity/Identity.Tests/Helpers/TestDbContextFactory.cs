using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Data;

namespace Identity.Tests.Helpers;

public static class TestDbContextFactory
{
    public static IdentityDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new IdentityDbContext(options);
    }
}
