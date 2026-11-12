using Microsoft.EntityFrameworkCore;
using Workspace.Repositories.Data;

namespace Workspace.Tests.Helpers;

public static class TestDbContextFactory
{
    public static WorkspaceDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new WorkspaceDbContext(options);
    }
}
