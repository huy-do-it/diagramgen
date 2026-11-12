using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace UMLForge.BuildingBlocks.Outbox;

/// <summary>
/// Mỗi giây lấy tối đa 50 dòng Outbox chưa gửi (theo thứ tự tạo) → XADD MAXLEN ~ 10000 → đánh dấu PublishedAt.
/// Có thể gửi trùng nếu sập giữa XADD và SaveChanges — consumer chống trùng bằng Inbox (at-least-once).
/// </summary>
internal sealed class OutboxPublisher<TDbContext> : BackgroundService
    where TDbContext : DbContext, IOutboxDbContext
{
    private const int BatchSize = 50;
    private const int MaxStreamLength = 10_000;
    private const int MaxErrorLength = 2000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OutboxPublisher<TDbContext>> _logger;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        ILogger<OutboxPublisher<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            do
            {
                try
                {
                    // Còn đầy batch nghĩa là còn tồn → gửi tiếp ngay, không chờ tick sau
                    while (await PublishBatchAsync(stoppingToken) == BatchSize)
                    {
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Outbox: lỗi khi gửi sự kiện, thử lại ở lượt sau");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host đang tắt
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var batch = await dbContext.OutboxMessages
            .Where(message => message.PublishedAt == null)
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);
        if (batch.Count == 0)
            return 0;

        var database = _redis.GetDatabase();
        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                await database.StreamAddAsync(
                    message.Stream,
                    [new NameValueEntry("type", message.Type), new NameValueEntry("envelope", message.Envelope)],
                    maxLength: MaxStreamLength,
                    useApproximateMaxLength: true);
                message.PublishedAt = DateTime.UtcNow;
                published++;
                _logger.LogInformation("Outbox → {Stream} {EventType} {EventId}", message.Stream, message.Type, message.Id);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
                _logger.LogWarning("Outbox: Redis lỗi khi gửi {EventType} {EventId} (lần {Attempts}): {Error}",
                    message.Type, message.Id, message.Attempts, ex.Message);
                // Dừng batch để giữ đúng thứ tự sự kiện
                break;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return published;
    }
}
