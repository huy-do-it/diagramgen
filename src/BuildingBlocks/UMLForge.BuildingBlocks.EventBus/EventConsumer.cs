using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.EventBus;

/// <summary>
/// Đọc 1 stream theo consumer group: XREADGROUP COUNT 10 &gt; (nghỉ 200 ms khi rỗng) → xử lý → XACK.
/// Lỗi → không ACK; định kỳ nhận lại message treo, giao quá <see cref="EventConsumerOptions.MaxDeliveries"/> lần → DLQ.
/// </summary>
internal sealed class EventConsumer<TDbContext> : BackgroundService
    where TDbContext : DbContext, IOutboxDbContext
{
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(2);

    private readonly EventConsumerOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly IStreamMaintenance _maintenance;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventConsumer<TDbContext>> _logger;
    private readonly string _consumerName = EventConsumerIdentity.Name;
    private bool _groupReady;

    public EventConsumer(
        EventConsumerOptions options,
        IConnectionMultiplexer redis,
        IStreamMaintenance maintenance,
        IServiceScopeFactory scopeFactory,
        ILogger<EventConsumer<TDbContext>> logger)
    {
        _options = options;
        _redis = redis;
        _maintenance = maintenance;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("Consumer {Group}/{Consumer} bắt đầu đọc {Stream} ({HandlerCount} loại sự kiện)",
            _options.Group, _consumerName, _options.Stream, _options.Handlers.Count);

        // Quét pending ngay khi khởi động để nhận lại message của instance cũ đã chết
        var nextRetryAt = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureGroupAsync();

                if (DateTime.UtcNow >= nextRetryAt)
                {
                    await RetryPendingAsync(stoppingToken);
                    nextRetryAt = DateTime.UtcNow + _options.RetryInterval;
                }

                var entries = await _redis.GetDatabase().StreamReadGroupAsync(
                    _options.Stream, _options.Group, _consumerName, StreamPosition.NewMessages, _options.BatchSize);
                if (entries.Length == 0)
                {
                    await Task.Delay(_options.PollInterval, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                    await ProcessAsync(entry, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Redis mất kết nối / group bị xóa → tạo lại group và thử lại sau
                _groupReady = false;
                _logger.LogWarning(ex, "Consumer {Group} lỗi khi đọc {Stream}, thử lại sau {Delay}s",
                    _options.Group, _options.Stream, ErrorBackoff.TotalSeconds);
                await DelaySafeAsync(ErrorBackoff, stoppingToken);
            }
        }
    }

    private async Task EnsureGroupAsync()
    {
        if (_groupReady)
            return;
        try
        {
            // Đọc từ đầu stream ("0-0") để không mất sự kiện phát ra trước khi service này chạy lần đầu;
            // sự kiện cũ đã xử lý sẽ bị Inbox bỏ qua.
            await _redis.GetDatabase().StreamCreateConsumerGroupAsync(
                _options.Stream, _options.Group, StreamPosition.Beginning, createStream: true);
            _logger.LogInformation("Đã tạo consumer group {Group} trên {Stream}", _options.Group, _options.Stream);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // Group đã tồn tại
        }

        _groupReady = true;
    }

    /// <summary>
    /// Message treo: giao ≥ MaxDeliveries lần → DLQ; của consumer khác treo quá MinIdleBeforeRetry → XCLAIM về mình;
    /// sau đó xử lý lại toàn bộ pending của chính mình.
    /// </summary>
    private async Task RetryPendingAsync(CancellationToken ct)
    {
        // Lấy mọi pending, không lọc idle: message của chính mình bị đọc lại mỗi RetryInterval nên Redis luôn
        // đặt lại idle về 0 — nếu lọc theo MinIdleBeforeRetry (60 s > 30 s) thì message lỗi không bao giờ tới DLQ
        var pending = await _maintenance.GetPendingAsync(_options.Stream, _options.Group, TimeSpan.Zero, ct);
        var minIdleMs = (long)_options.MinIdleBeforeRetry.TotalMilliseconds;

        // Của mình: xử lý tuần tự nên không có gì đang chạy dở; của consumer khác: chỉ đụng khi đã treo đủ lâu
        bool IsStale(PendingEntry entry) => entry.Consumer == _consumerName || entry.IdleMs >= minIdleMs;

        foreach (var entry in pending.Where(p => p.DeliveryCount >= _options.MaxDeliveries && IsStale(p)))
        {
            await _maintenance.MoveToDeadLetterAsync(_options.Stream, _options.Group, entry.Id, ct);
            _logger.LogWarning("Chuyển {MessageId} sang {DeadLetter} sau {Deliveries} lần giao lỗi",
                entry.Id, StreamNames.DeadLetter(_options.Stream), entry.DeliveryCount);
        }

        var fromOthers = pending
            .Where(p => p.DeliveryCount < _options.MaxDeliveries && p.Consumer != _consumerName && p.IdleMs >= minIdleMs)
            .Select(p => p.Id)
            .ToList();
        if (fromOthers.Count > 0)
        {
            await _maintenance.ClaimAsync(_options.Stream, _options.Group, _consumerName, fromOthers, ct);
            _logger.LogInformation("Nhận lại {Count} message treo của consumer khác trên {Stream}",
                fromOthers.Count, _options.Stream);
        }

        // Đọc lịch sử pending của chính mình (ID "0") — gồm message vừa claim và message lần trước lỗi
        var own = await _redis.GetDatabase().StreamReadGroupAsync(
            _options.Stream, _options.Group, _consumerName, StreamPosition.Beginning, count: 50);
        foreach (var entry in own)
        {
            if (entry.Values.Length == 0)
            {
                // Message đã bị cắt khỏi stream (MAXLEN) → chỉ còn id, ACK cho sạch
                await AckAsync(entry.Id);
                continue;
            }

            await ProcessAsync(entry, ct);
        }
    }

    private async Task ProcessAsync(StreamEntry entry, CancellationToken ct)
    {
        var type = entry["type"];
        var envelope = entry["envelope"];
        if (type.IsNullOrEmpty || envelope.IsNullOrEmpty)
        {
            _logger.LogError("Message {MessageId} trên {Stream} thiếu field type/envelope", entry.Id, _options.Stream);
            await _maintenance.MoveToDeadLetterAsync(_options.Stream, _options.Group, entry.Id!, ct);
            return;
        }

        if (!_options.Handlers.TryGetValue(type.ToString(), out var dispatcher))
        {
            // Sự kiện không đăng ký → ACK, bỏ qua
            await AckAsync(entry.Id);
            return;
        }

        try
        {
            await dispatcher(_scopeFactory, _logger, envelope.ToString(), ct);
            await AckAsync(entry.Id);
        }
        catch (InvalidEventException ex)
        {
            _logger.LogError(ex, "Message {MessageId} ({EventType}) hỏng, chuyển DLQ", entry.Id, type.ToString());
            await _maintenance.MoveToDeadLetterAsync(_options.Stream, _options.Group, entry.Id!, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Xử lý {EventType} ({MessageId}) lỗi — giữ lại để thử lại", type.ToString(), entry.Id);
        }
    }

    private Task<long> AckAsync(RedisValue messageId)
        => _redis.GetDatabase().StreamAcknowledgeAsync(_options.Stream, _options.Group, messageId);

    private static async Task DelaySafeAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Host đang tắt
        }
    }
}
