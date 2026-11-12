using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Identity.Repositories.Data;

namespace Identity.Service.Jobs;

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TimeSpan _pollingInterval;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisher> logger,
        IConnectionMultiplexer? redis = null)
        : this(scopeFactory, logger, redis, TimeSpan.FromSeconds(1))
    {
    }

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisher> logger,
        IConnectionMultiplexer? redis,
        TimeSpan pollingInterval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _redis = redis;
        _pollingInterval = pollingInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher started with polling interval {Interval}s.", _pollingInterval.TotalSeconds);

        using var timer = new PeriodicTimer(_pollingInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Unexpected error in OutboxPublisher background loop. Retrying next cycle.");
            }
        }

        _logger.LogInformation("OutboxPublisher stopped.");
    }

    public async Task<int> PublishPendingAsync(CancellationToken ct = default)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogDebug("Redis connection is unavailable. Skipping Outbox publication cycle.");
            return 0;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        var redisDb = _redis.GetDatabase();
        var publishedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                var entries = new NameValueEntry[]
                {
                    new("type", message.Type),
                    new("envelope", message.Envelope)
                };

                // XADD MAXLEN ~ 10000 * type <EventType> envelope <JSON>
                await redisDb.StreamAddAsync(
                    message.Stream,
                    entries,
                    maxLength: 10000,
                    useApproximateMaxLength: true
                );

                message.PublishedAt = DateTime.UtcNow;
                message.LastError = null;
                publishedCount++;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                _logger.LogWarning(ex, "Failed to publish Outbox message {Id} ({Type}) to stream '{Stream}'. Will retry.",
                    message.Id, message.Type, message.Stream);
                break; // Stop batch to preserve FIFO order in stream
            }
        }

        await db.SaveChangesAsync(ct);
        return publishedCount;
    }
}
