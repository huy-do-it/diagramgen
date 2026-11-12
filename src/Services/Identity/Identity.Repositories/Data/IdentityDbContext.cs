using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Entities;

namespace Identity.Repositories.Data;

public class IdentityDbContext : DbContext, IOutboxDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
            builder.HasIndex(u => u.Username).IsUnique();
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            builder.Property(u => u.Role).IsRequired().HasMaxLength(20);
            builder.Property(u => u.IsLocked).IsRequired().HasDefaultValue(false);
            builder.Property(u => u.CreatedAt).IsRequired();

            builder.HasMany(u => u.RefreshTokens)
                   .WithOne(r => r.User)
                   .HasForeignKey(r => r.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken entity configuration
        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.ToTable("RefreshTokens");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(128);
            builder.HasIndex(r => r.TokenHash).IsUnique();
            builder.Property(r => r.ExpiresAt).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();
        });

        // OutboxMessage entity configuration
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Stream).IsRequired().HasMaxLength(100);
            builder.Property(o => o.Type).IsRequired().HasMaxLength(100);
            builder.Property(o => o.Envelope).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.HasIndex(o => o.PublishedAt);
        });

        // InboxMessage entity configuration
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Stream).IsRequired().HasMaxLength(100);
            builder.HasIndex(i => new { i.EventId, i.Stream }).IsUnique();
        });
    }
}
