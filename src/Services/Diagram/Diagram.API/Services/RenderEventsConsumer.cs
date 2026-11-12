using System.Text.Json;
using StackExchange.Redis;
using UMLForge.Diagram.Service.EventHandlers;
using UMLForge.Diagram.Service.Events;

namespace UMLForge.Diagram.API.Services;

public class RenderEventsConsumer : BackgroundService
{
    private const string StreamName = StreamNames.Render;
    private const string GroupName = "diagram-svc";
    private readonly string _consumerName = $"diagram-{Environment.MachineName}-{Guid.NewGuid().ToString()[..8]}";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RenderEventsConsumer> _logger;
    private IConnectionMultiplexer? _redis;

    public RenderEventsConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RenderEventsConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RenderEventsConsumer started for stream {Stream}, group {Group}.", StreamName, GroupName);

        var redisUrl = _configuration["REDIS_URL"] ?? _configuration.GetConnectionString("Redis") ?? "localhost:6379";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var redis = await GetRedisConnectionAsync(redisUrl);
                if (redis == null || !redis.IsConnected)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                var db = redis.GetDatabase();
                await EnsureConsumerGroupAsync(db);

                var entries = await db.StreamReadGroupAsync(StreamName, GroupName, _consumerName, ">", count: 10);
                if (entries == null || entries.Length == 0)
                {
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(db, entry, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing render.events stream.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _redis?.Dispose();
        _logger.LogInformation("RenderEventsConsumer stopped.");
    }

    private async Task ProcessEntryAsync(IDatabase db, StreamEntry entry, CancellationToken ct)
    {
        var typeEntry = entry["type"];
        var envelopeEntry = entry["envelope"];

        if (envelopeEntry.IsNullOrEmpty)
        {
            await db.StreamAcknowledgeAsync(StreamName, GroupName, entry.Id);
            return;
        }

        var envelopeJson = envelopeEntry.ToString();
        var eventType = typeEntry.HasValue ? typeEntry.ToString() : string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRenderEventsHandler>();

        try
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            switch (eventType)
            {
                case "RenderStarted":
                    var started = JsonSerializer.Deserialize<EventEnvelope<RenderStarted>>(envelopeJson, jsonOptions);
                    if (started != null) await handler.HandleRenderStartedAsync(started, ct);
                    break;

                case "RenderCompleted":
                    var completed = JsonSerializer.Deserialize<EventEnvelope<RenderCompleted>>(envelopeJson, jsonOptions);
                    if (completed != null) await handler.HandleRenderCompletedAsync(completed, ct);
                    break;

                case "RenderFailed":
                    var failed = JsonSerializer.Deserialize<EventEnvelope<RenderFailed>>(envelopeJson, jsonOptions);
                    if (failed != null) await handler.HandleRenderFailedAsync(failed, ct);
                    break;

                default:
                    _logger.LogDebug("Render event {Type} ignored by diagram-svc.", eventType);
                    break;
            }

            await db.StreamAcknowledgeAsync(StreamName, GroupName, entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle render event {Id} of type {Type}.", entry.Id, eventType);
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamName, GroupName, "$", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists
        }
    }

    private async Task<IConnectionMultiplexer?> GetRedisConnectionAsync(string redisUrl)
    {
        if (_redis != null && _redis.IsConnected) return _redis;
        try
        {
            var options = ConfigurationOptions.Parse(redisUrl);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            _redis = await ConnectionMultiplexer.ConnectAsync(options);
            return _redis;
        }
        catch
        {
            return null;
        }
    }
}
