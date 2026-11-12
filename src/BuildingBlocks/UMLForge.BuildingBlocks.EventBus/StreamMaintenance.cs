using StackExchange.Redis;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.EventBus;

/// <summary>Message đã giao nhưng chưa ACK (XPENDING).</summary>
public sealed record PendingEntry(string Id, string Consumer, long IdleMs, int DeliveryCount);

/// <summary>Thao tác bảo trì stream cho ReconcileJob: xem pending, nhận lại message, chuyển DLQ.</summary>
public interface IStreamMaintenance
{
    Task<IReadOnlyList<PendingEntry>> GetPendingAsync(string stream, string group, TimeSpan minIdle, CancellationToken ct);

    Task ClaimAsync(string stream, string group, string consumer, IEnumerable<string> ids, CancellationToken ct);

    Task MoveToDeadLetterAsync(string stream, string group, string id, CancellationToken ct);
}

/// <summary>Tên consumer của process hiện tại trong mọi consumer group (mỗi container 1 tên).</summary>
public static class EventConsumerIdentity
{
    public static string Name { get; } = $"{Environment.MachineName}-{Environment.ProcessId}";
}

internal sealed class RedisStreamMaintenance : IStreamMaintenance
{
    private const int PendingPageSize = 100;
    private const int MaxDeadLetterLength = 10_000;

    private readonly IConnectionMultiplexer _redis;

    public RedisStreamMaintenance(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IReadOnlyList<PendingEntry>> GetPendingAsync(
        string stream, string group, TimeSpan minIdle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var pending = await _redis.GetDatabase()
            .StreamPendingMessagesAsync(stream, group, PendingPageSize, RedisValue.Null);
        return pending
            .Where(info => info.IdleTimeInMilliseconds >= (long)minIdle.TotalMilliseconds)
            .Select(info => new PendingEntry(
                info.MessageId.ToString(), info.ConsumerName.ToString(), info.IdleTimeInMilliseconds, info.DeliveryCount))
            .ToList();
    }

    public async Task ClaimAsync(string stream, string group, string consumer, IEnumerable<string> ids, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var messageIds = ids.Select(id => (RedisValue)id).ToArray();
        if (messageIds.Length == 0)
            return;
        await _redis.GetDatabase().StreamClaimIdsOnlyAsync(stream, group, consumer, 0, messageIds);
    }

    public async Task MoveToDeadLetterAsync(string stream, string group, string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var database = _redis.GetDatabase();
        var entries = await database.StreamRangeAsync(stream, id, id, count: 1);
        if (entries.Length > 0)
        {
            NameValueEntry[] fields =
            [
                .. entries[0].Values,
                new NameValueEntry("sourceId", id),
                new NameValueEntry("group", group),
                new NameValueEntry("deadAt", DateTime.UtcNow.ToString("O")),
            ];
            await database.StreamAddAsync(StreamNames.DeadLetter(stream), fields,
                maxLength: MaxDeadLetterLength, useApproximateMaxLength: true);
        }

        await database.StreamAcknowledgeAsync(stream, group, id);
    }
}
