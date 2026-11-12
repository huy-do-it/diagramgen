using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMLForge.Notification.Repositories;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb")
            ?? "Server=localhost;Database=NotificationDb;Integrated Security=True;TrustServerCertificate=True";
        return new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(connection).Options);
    }
}
