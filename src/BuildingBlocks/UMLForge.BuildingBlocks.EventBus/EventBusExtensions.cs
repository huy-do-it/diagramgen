using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.EventBus;

public static class EventBusExtensions
{
    /// <summary>
    /// Thêm 1 consumer cho (stream, group). Gọi nhiều lần cho nhiều stream được.
    /// <code>
    /// services.AddEventConsumer&lt;DiagramDbContext&gt;(StreamNames.Workspace, ConsumerGroups.DiagramSvc)
    ///         .On&lt;MemberAdded, MemberAddedHandler&gt;();
    /// </code>
    /// </summary>
    public static EventConsumerBuilder<TDbContext> AddEventConsumer<TDbContext>(
        this IServiceCollection services, string stream, string group)
        where TDbContext : DbContext, IOutboxDbContext
    {
        services.AddUmlForgeRedis();
        services.TryAddSingleton<IStreamMaintenance, RedisStreamMaintenance>();

        var options = new EventConsumerOptions(stream, group);
        // Đăng ký bằng factory để nhiều consumer cùng kiểu EventConsumer<TDbContext> không bị gộp làm một
        services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<EventConsumer<TDbContext>>(sp, options));
        return new EventConsumerBuilder<TDbContext>(services, options);
    }
}

public sealed class EventConsumerBuilder<TDbContext>
    where TDbContext : DbContext, IOutboxDbContext
{
    private readonly IServiceCollection _services;
    private readonly EventConsumerOptions _options;

    internal EventConsumerBuilder(IServiceCollection services, EventConsumerOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>Gắn handler cho 1 loại sự kiện. Sự kiện không đăng ký sẽ được ACK và bỏ qua.</summary>
    public EventConsumerBuilder<TDbContext> On<TEvent, THandler>()
        where TEvent : class
        where THandler : class, IEventHandler<TEvent>
    {
        var eventName = EventTypeRegistry.NameOf<TEvent>();
        var eventStream = EventTypeRegistry.StreamOf(eventName);
        if (!string.Equals(eventStream, _options.Stream, StringComparison.Ordinal))
            throw new InvalidOperationException($"Sự kiện '{eventName}' thuộc stream '{eventStream}', không phải '{_options.Stream}'.");

        _services.TryAddScoped<THandler>();
        var group = _options.Group;
        _options.Handlers[eventName] = (scopeFactory, logger, json, ct)
            => EventDispatch.DispatchAsync<TDbContext, TEvent, THandler>(scopeFactory, logger, group, json, ct);
        return this;
    }

    /// <summary>Đổi thông số poll/retry/DLQ (chủ yếu để test).</summary>
    public EventConsumerBuilder<TDbContext> Configure(Action<EventConsumerOptions> configure)
    {
        configure(_options);
        return this;
    }
}
