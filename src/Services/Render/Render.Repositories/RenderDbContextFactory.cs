using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMLForge.Render.Repositories;

public sealed class RenderDbContextFactory : IDesignTimeDbContextFactory<RenderDbContext>
{
    public RenderDbContext CreateDbContext(string[] args)
    {
        // Chỉ dùng để sinh migration; không mở kết nối hoặc chứa secret.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RenderDb")
            ?? "Server=localhost;Database=RenderDb;Integrated Security=True;TrustServerCertificate=True";
        return new RenderDbContext(new DbContextOptionsBuilder<RenderDbContext>()
            .UseSqlServer(connection).Options);
    }
}
