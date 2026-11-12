using Microsoft.Extensions.Configuration;
using Identity.Repositories.Entities;
using Identity.Repositories.Repositories;
using UMLForge.Contracts.Events;

namespace Identity.Repositories.Data;

public static class IdentityDbSeeder
{
    public static async Task SeedAsync(IdentityDbContext context, IConfiguration configuration)
    {
        if (context.Users.Any())
        {
            return;
        }

        var defaultPassword = configuration["Seed:DefaultPassword"] ?? "Password123@";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword, workFactor: 11);

        var seedUsers = new List<User>
        {
            new() { Id = Guid.NewGuid(), Username = "admin", PasswordHash = passwordHash, FullName = "System Administrator", Role = "Admin", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "lecturer1", PasswordHash = passwordHash, FullName = "Dr. Nguyen Van A", Role = "Lecturer", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "lecturer2", PasswordHash = passwordHash, FullName = "Dr. Tran Thi B", Role = "Lecturer", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "student1", PasswordHash = passwordHash, FullName = "Le Van C", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "student2", PasswordHash = passwordHash, FullName = "Pham Thi D", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "student3", PasswordHash = passwordHash, FullName = "Hoang Van E", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "student4", PasswordHash = passwordHash, FullName = "Vu Thi F", Role = "Student", IsLocked = false, CreatedAt = DateTime.UtcNow }
        };

        var outbox = new Outbox(context);

        foreach (var user in seedUsers)
        {
            context.Users.Add(user);
            outbox.Add(StreamNames.Identity, new UserRegistered(user.Id, user.Username, user.FullName, user.Role));
        }

        await context.SaveChangesAsync();
    }
}
