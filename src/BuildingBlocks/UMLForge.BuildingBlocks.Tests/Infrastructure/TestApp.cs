using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Tests.Infrastructure;

/// <summary>DbContext tối thiểu của 1 "service": 1 bảng nghiệp vụ + Outbox/Inbox.</summary>
public sealed class TestDbContext : DbContext, IOutboxDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemberAccess> MemberAccess => Set<MemberAccess>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberAccess>().HasKey(access => new { access.ProjectId, access.UserId });
        modelBuilder.ApplyOutboxInbox();
    }
}

public sealed class MemberAccess
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public string Role { get; set; } = string.Empty;
}

/// <summary>Đếm số lần handler chạy (singleton trong từng host test).</summary>
public sealed class HandlerProbe
{
    private int _memberAdded;
    private int _memberRemovedAttempts;

    public int MemberAdded => _memberAdded;

    public int MemberRemovedAttempts => _memberRemovedAttempts;

    public ConcurrentBag<Guid> CorrelationIds { get; } = [];

    public void OnMemberAdded(Guid correlationId)
    {
        Interlocked.Increment(ref _memberAdded);
        CorrelationIds.Add(correlationId);
    }

    public void OnMemberRemoved() => Interlocked.Increment(ref _memberRemovedAttempts);
}

/// <summary>Handler ghi bản sao MemberAccess — giống consumer của Diagram service (T-04).</summary>
public sealed class MemberAddedHandler : IEventHandler<MemberAdded>
{
    private readonly TestDbContext _dbContext;
    private readonly HandlerProbe _probe;

    public MemberAddedHandler(TestDbContext dbContext, HandlerProbe probe)
    {
        _dbContext = dbContext;
        _probe = probe;
    }

    public Task HandleAsync(EventEnvelope<MemberAdded> e, CancellationToken ct)
    {
        _probe.OnMemberAdded(e.CorrelationId);
        _dbContext.MemberAccess.Add(new MemberAccess { ProjectId = e.Payload.ProjectId, UserId = e.Payload.UserId, Role = e.Payload.Role });
        return Task.CompletedTask;
    }
}

/// <summary>Handler luôn lỗi — giả lập lỗi hạ tầng tạm thời.</summary>
public sealed class FailingMemberRemovedHandler : IEventHandler<MemberRemoved>
{
    private readonly HandlerProbe _probe;

    public FailingMemberRemovedHandler(HandlerProbe probe)
    {
        _probe = probe;
    }

    public Task HandleAsync(EventEnvelope<MemberRemoved> e, CancellationToken ct)
    {
        _probe.OnMemberRemoved();
        throw new InvalidOperationException("Giả lập SQL tạm thời không phản hồi");
    }
}

/// <summary>Host thật (Outbox + 1 consumer workspace.events) chạy trên container của fixture.</summary>
public sealed class TestApp : IAsyncDisposable
{
    private readonly IHost _host;

    private TestApp(IHost host)
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public HandlerProbe Probe => _host.Services.GetRequiredService<HandlerProbe>();

    public IDatabase Redis => _host.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

    public static async Task<TestApp> StartAsync(
        ContainersFixture containers, Action<EventConsumerOptions>? configure = null, bool flushRedis = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = containers.RedisConnectionString + ",allowAdmin=true",
        });

        var database = $"bb_{Guid.NewGuid():N}";
        builder.Services.AddDbContext<TestDbContext>(options => options.UseSqlServer(containers.SqlConnectionString(database)));
        builder.Services.AddSingleton<HandlerProbe>();
        builder.Services.AddOutbox<TestDbContext>(Producers.Workspace);
        builder.Services.AddEventConsumer<TestDbContext>(StreamNames.Workspace, ConsumerGroups.DiagramSvc)
            .On<MemberAdded, MemberAddedHandler>()
            .On<MemberRemoved, FailingMemberRemovedHandler>()
            .Configure(options =>
            {
                options.RetryInterval = TimeSpan.FromSeconds(1);
                options.MinIdleBeforeRetry = TimeSpan.FromMilliseconds(500);
                configure?.Invoke(options);
            });

        var host = builder.Build();

        // Mỗi test bắt đầu với Redis sạch và database mới
        if (flushRedis)
            await host.Services.GetRequiredService<IConnectionMultiplexer>().GetServer(containers.RedisConnectionString).FlushAllDatabasesAsync();
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();

        await host.StartAsync();
        return new TestApp(host);
    }

    /// <summary>Ghi nghiệp vụ + Outbox trong cùng 1 SaveChanges, giống service thật.</summary>
    public async Task PublishAsync<T>(string stream, T payload, Guid? correlationId = null) where T : class
    {
        using var activity = correlationId is { } id ? CorrelationContext.Begin(id, "test") : null;
        await using var scope = _host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IOutbox>().Add(stream, payload);
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync();
    }

    public async Task<TResult> QueryAsync<TResult>(Func<TestDbContext, Task<TResult>> query)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}

public static class Eventually
{
    /// <summary>Chờ điều kiện đúng (xử lý bất đồng bộ), quá thời gian thì trả false.</summary>
    public static async Task<bool> TrueAsync(Func<Task<bool>> condition, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(100);
        }

        return await condition();
    }

    public static Task<bool> TrueAsync(Func<bool> condition, int timeoutSeconds = 15)
        => TrueAsync(() => Task.FromResult(condition()), timeoutSeconds);
}
