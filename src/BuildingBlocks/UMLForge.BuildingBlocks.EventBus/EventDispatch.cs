using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.EventBus;

internal enum DispatchResult
{
    Processed,
    Duplicate,
}

internal delegate Task<DispatchResult> EventDispatcher(
    IServiceScopeFactory scopeFactory, ILogger logger, string envelopeJson, CancellationToken ct);

/// <summary>Envelope không đọc được — lỗi vĩnh viễn, đưa thẳng vào DLQ.</summary>
internal sealed class InvalidEventException : Exception
{
    public InvalidEventException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

internal static class EventDispatch
{
    /// <summary>
    /// Deserialize → kiểm Inbox → gọi handler → thêm Inbox → SaveChanges (handler + Inbox trong 1 lần lưu).
    /// </summary>
    public static async Task<DispatchResult> DispatchAsync<TDbContext, TEvent, THandler>(
        IServiceScopeFactory scopeFactory, ILogger logger, string group, string envelopeJson, CancellationToken ct)
        where TDbContext : DbContext, IOutboxDbContext
        where TEvent : class
        where THandler : class, IEventHandler<TEvent>
    {
        EventEnvelope<TEvent>? envelope;
        try
        {
            envelope = EventJson.Deserialize<TEvent>(envelopeJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidEventException($"Envelope {typeof(TEvent).Name} sai định dạng JSON: {ex.Message}", ex);
        }

        if (envelope is null || envelope.Payload is null || envelope.EventId == Guid.Empty)
            throw new InvalidEventException($"Envelope {typeof(TEvent).Name} thiếu eventId hoặc payload.");

        using var activity = CorrelationContext.Begin(envelope.CorrelationId, "umlforge.event");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationKeys.LogProperty] = envelope.CorrelationId,
            ["EventId"] = envelope.EventId,
            ["EventType"] = envelope.Type,
        });

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        if (await IsProcessedAsync(dbContext, envelope.EventId, group, ct))
        {
            logger.LogInformation("Bỏ qua {EventType} {EventId}: đã xử lý (Inbox)", envelope.Type, envelope.EventId);
            return DispatchResult.Duplicate;
        }

        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        await handler.HandleAsync(envelope, ct);

        dbContext.InboxMessages.Add(new InboxMessage
        {
            EventId = envelope.EventId,
            Consumer = group,
            ProcessedAt = DateTime.UtcNow,
        });

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Instance khác vừa xử lý xong cùng sự kiện (sau XCLAIM) → coi như trùng; còn lại thì ném tiếp
            if (!await IsProcessedInNewScopeAsync<TDbContext>(scopeFactory, envelope.EventId, group, ct))
                throw;
            return DispatchResult.Duplicate;
        }

        logger.LogInformation("Đã xử lý {EventType} {EventId}", envelope.Type, envelope.EventId);
        return DispatchResult.Processed;
    }

    private static Task<bool> IsProcessedAsync<TDbContext>(TDbContext dbContext, Guid eventId, string group, CancellationToken ct)
        where TDbContext : DbContext, IOutboxDbContext
        => dbContext.InboxMessages.AsNoTracking()
            .AnyAsync(message => message.EventId == eventId && message.Consumer == group, ct);

    private static async Task<bool> IsProcessedInNewScopeAsync<TDbContext>(
        IServiceScopeFactory scopeFactory, Guid eventId, string group, CancellationToken ct)
        where TDbContext : DbContext, IOutboxDbContext
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await IsProcessedAsync(dbContext, eventId, group, ct);
    }
}
