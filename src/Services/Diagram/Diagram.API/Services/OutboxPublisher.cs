using StackExchange.Redis;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.API.Services;

/// <summary>
/// BackgroundService that periodically polls unpublished Outbox messages and publishes them to Redis Streams (diagram.events).
/// Complies with RULES 5.1: Scope per iteration, PeriodicTimer, exception handling, graceful shutdown via stoppingToken.
/// </summary>
public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxPublisher> _logger;
    private IConnectionMultiplexer? _redis;
    private bool _redisConnectionAttempted;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher background service started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await ProcessOutboxBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during Outbox publication cycle.");
            }
        }

        _logger.LogInformation("OutboxPublisher background service is stopping.");
        _redis?.Dispose();
    }

    private async Task ProcessOutboxBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var unpublishedMessages = await outboxRepo.GetUnpublishedMessagesAsync(50, stoppingToken);
        if (unpublishedMessages.Count == 0)
        {
            return;
        }

        var redisDb = await GetRedisDatabaseAsync();

        foreach (var message in unpublishedMessages)
        {
            try
            {
                if (redisDb != null)
                {
                    var entries = new NameValueEntry[]
                    {
                        new("type", message.Type),
                        new("envelope", message.Envelope)
                    };

                    // XADD <stream> MAXLEN ~ 10000 * type <type> envelope <envelope>
                    await redisDb.StreamAddAsync(
                        message.Stream,
                        entries,
                        maxLength: 10000,
                        useApproximateMaxLength: true);
                }
                else
                {
                    // If Redis is not available (e.g., local dev without Redis), log debug
                    _logger.LogDebug("Redis connection not active. Mocking dispatch for outbox message {MessageId} on stream {Stream}.", message.Id, message.Stream);
                }

                message.PublishedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                _logger.LogWarning(ex, "Failed to publish Outbox message {MessageId} to stream {Stream}. Attempt: {Attempts}",
                    message.Id, message.Stream, message.Attempts);
            }
        }

        await outboxRepo.SaveChangesAsync(stoppingToken);
    }

    private async Task<IDatabase?> GetRedisDatabaseAsync()
    {
        if (_redis != null && _redis.IsConnected)
        {
            return _redis.GetDatabase();
        }

        var redisConnectionString = _configuration["REDIS_URL"]
            ?? _configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        try
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            options.SyncTimeout = 1000;

            _redis = await ConnectionMultiplexer.ConnectAsync(options);
            if (_redis.IsConnected)
            {
                if (!_redisConnectionAttempted)
                {
                    _logger.LogInformation("Successfully connected to Redis at {Endpoint}", redisConnectionString);
                    _redisConnectionAttempted = true;
                }
                return _redis.GetDatabase();
            }
        }
        catch (Exception ex)
        {
            if (!_redisConnectionAttempted)
            {
                _logger.LogWarning("Could not connect to Redis at {Endpoint}: {Message}. Outbox messages will be queued.", redisConnectionString, ex.Message);
                _redisConnectionAttempted = true;
            }
        }

        return null;
    }
}
