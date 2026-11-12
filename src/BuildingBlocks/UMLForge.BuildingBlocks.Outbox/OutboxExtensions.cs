using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace UMLForge.BuildingBlocks.Outbox;

public static class OutboxExtensions
{
    /// <summary>Cấu hình bảng OutboxMessages + InboxMessages; gọi trong <c>OnModelCreating</c>.</summary>
    public static ModelBuilder ApplyOutboxInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Stream).HasMaxLength(100).IsRequired();
            entity.Property(message => message.Type).HasMaxLength(100).IsRequired();
            entity.Property(message => message.Envelope).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => new { message.PublishedAt, message.CreatedAt });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(message => new { message.EventId, message.Consumer });
            entity.Property(message => message.Consumer).HasMaxLength(100);
            entity.HasIndex(message => message.ProcessedAt);
        });

        return modelBuilder;
    }

    /// <summary>Đăng ký <see cref="IOutbox"/> (scoped, dùng chung DbContext với nghiệp vụ) + OutboxPublisher.</summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services, string producer)
        where TDbContext : DbContext, IOutboxDbContext
    {
        services.AddUmlForgeRedis();
        services.AddSingleton(new OutboxSettings<TDbContext>(producer));
        services.TryAddScoped<IOutbox, Outbox<TDbContext>>();
        services.AddHostedService<OutboxPublisher<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Kết nối Redis dùng chung (singleton) từ <c>ConnectionStrings:Redis</c>, ví dụ "localhost:6379".
    /// Redis chưa sẵn sàng vẫn khởi động được và tự kết nối lại.
    /// </summary>
    public static IServiceCollection AddUmlForgeRedis(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Thiếu cấu hình 'ConnectionStrings:Redis' (ví dụ localhost:6379).");
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        return services;
    }
}
