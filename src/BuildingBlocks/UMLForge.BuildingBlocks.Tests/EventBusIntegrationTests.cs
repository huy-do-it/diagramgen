using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UMLForge.BuildingBlocks.Tests.Infrastructure;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Tests;

/// <summary>B-02 mục 5: Outbox → Redis Streams → EventConsumer với Redis + SQL Server thật.</summary>
[Collection(ContainersCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EventBusIntegrationTests
{
    private readonly ContainersFixture _containers;

    public EventBusIntegrationTests(ContainersFixture containers)
    {
        _containers = containers;
    }

    private static MemberAdded NewMemberAdded() =>
        new(Guid.NewGuid(), "CampusEats", Guid.NewGuid(), ProjectRoles.Member, Guid.NewGuid());

    [Fact]
    public async Task Publish_OneEventThroughOutbox_HandledExactlyOnce()
    {
        await using var app = await TestApp.StartAsync(_containers);
        var correlationId = Guid.NewGuid();
        var payload = NewMemberAdded();

        await app.PublishAsync(StreamNames.Workspace, payload, correlationId);

        Assert.True(await Eventually.TrueAsync(() => app.Probe.MemberAdded == 1));
        Assert.True(await Eventually.TrueAsync(async () =>
            (await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc)).PendingMessageCount == 0));
        Assert.Equal(1, app.Probe.MemberAdded);
        Assert.Equal(correlationId, Assert.Single(app.Probe.CorrelationIds));

        // Outbox đã đánh dấu gửi; thay đổi của handler + Inbox đã lưu cùng nhau
        Assert.True(await app.QueryAsync(db => db.OutboxMessages.AllAsync(message => message.PublishedAt != null)));
        Assert.True(await app.QueryAsync(db => db.MemberAccess.AnyAsync(access => access.UserId == payload.UserId)));
        Assert.Equal(1, await app.QueryAsync(db => db.InboxMessages.CountAsync(inbox => inbox.Consumer == ConsumerGroups.DiagramSvc)));
    }

    [Fact]
    public async Task Publish_SameEventIdTwice_HandlerRunsOnce()
    {
        await using var app = await TestApp.StartAsync(_containers);
        await app.PublishAsync(StreamNames.Workspace, NewMemberAdded());
        Assert.True(await Eventually.TrueAsync(() => app.Probe.MemberAdded == 1));

        // Gửi lại đúng message đó (cùng eventId) — giống publisher gửi trùng sau khi sập
        var original = (await app.Redis.StreamRangeAsync(StreamNames.Workspace)).Single();
        await app.Redis.StreamAddAsync(StreamNames.Workspace, original.Values);

        Assert.True(await Eventually.TrueAsync(async () =>
        {
            var groups = await app.Redis.StreamGroupInfoAsync(StreamNames.Workspace);
            var pending = await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc);
            return groups.Single().LastDeliveredId == (await app.Redis.StreamRangeAsync(StreamNames.Workspace)).Last().Id
                && pending.PendingMessageCount == 0;
        }));
        Assert.Equal(1, app.Probe.MemberAdded);
        Assert.Equal(1, await app.QueryAsync(db => db.MemberAccess.CountAsync()));
    }

    [Fact]
    public async Task Handle_HandlerThrows_MessageStaysPendingAndNotInInbox()
    {
        // Retry rất thưa để quan sát trạng thái ngay sau lần giao đầu tiên
        await using var app = await TestApp.StartAsync(_containers, options => options.RetryInterval = TimeSpan.FromMinutes(5));

        await app.PublishAsync(StreamNames.Workspace, new MemberRemoved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(await Eventually.TrueAsync(() => app.Probe.MemberRemovedAttempts >= 1));
        var pending = await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc);
        Assert.Equal(1, pending.PendingMessageCount);
        Assert.Equal(0, await app.QueryAsync(db => db.InboxMessages.CountAsync()));
    }

    [Fact]
    public async Task Handle_HandlerKeepsFailing_MovedToDeadLetterAfterMaxDeliveries()
    {
        await using var app = await TestApp.StartAsync(_containers, options => options.MaxDeliveries = 3);

        await app.PublishAsync(StreamNames.Workspace, new MemberRemoved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var deadLetter = StreamNames.DeadLetter(StreamNames.Workspace);
        Assert.True(await Eventually.TrueAsync(async () => await app.Redis.StreamLengthAsync(deadLetter) == 1, timeoutSeconds: 30));
        var dead = (await app.Redis.StreamRangeAsync(deadLetter)).Single();
        Assert.Equal(nameof(MemberRemoved), dead["type"].ToString());
        Assert.Equal(ConsumerGroups.DiagramSvc, dead["group"].ToString());
        Assert.Equal(0, (await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc)).PendingMessageCount);
    }

    [Fact]
    public async Task Handle_HandlerKeepsFailingAndMinIdleLongerThanRetryInterval_MovedToDeadLetter()
    {
        // Giống mặc định (MinIdle 60 s > RetryInterval 30 s) nhưng rút ngắn để test chạy nhanh
        await using var app = await TestApp.StartAsync(_containers, options =>
        {
            options.RetryInterval = TimeSpan.FromSeconds(1);
            options.MinIdleBeforeRetry = TimeSpan.FromSeconds(10);
            options.MaxDeliveries = 3;
        });

        await app.PublishAsync(StreamNames.Workspace, new MemberRemoved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var deadLetter = StreamNames.DeadLetter(StreamNames.Workspace);
        Assert.True(await Eventually.TrueAsync(async () => await app.Redis.StreamLengthAsync(deadLetter) == 1, timeoutSeconds: 20));
        Assert.Equal(0, (await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc)).PendingMessageCount);
    }

    [Fact]
    public async Task Handle_InvalidEnvelope_MovedToDeadLetterWithoutRetry()
    {
        await using var app = await TestApp.StartAsync(_containers);

        await app.Redis.StreamAddAsync(StreamNames.Workspace,
            [new NameValueEntry("type", nameof(MemberAdded)), new NameValueEntry("envelope", "{not json")]);

        var deadLetter = StreamNames.DeadLetter(StreamNames.Workspace);
        Assert.True(await Eventually.TrueAsync(async () => await app.Redis.StreamLengthAsync(deadLetter) == 1));
        Assert.Equal(0, app.Probe.MemberAdded);
    }

    [Fact]
    public async Task Handle_UnregisteredEventType_AcknowledgedAndSkipped()
    {
        await using var app = await TestApp.StartAsync(_containers);
        var envelope = EventEnvelope.Create(Producers.Workspace, new ProjectDeleted(Guid.NewGuid(), Guid.NewGuid()));

        await app.Redis.StreamAddAsync(StreamNames.Workspace,
            [new NameValueEntry("type", envelope.Type), new NameValueEntry("envelope", EventJson.Serialize(envelope))]);

        Assert.True(await Eventually.TrueAsync(async () =>
        {
            var groups = await app.Redis.StreamGroupInfoAsync(StreamNames.Workspace);
            return groups.Length == 1 && groups[0].EntriesRead == 1 && groups[0].PendingMessageCount == 0;
        }));
    }

    [Fact]
    public async Task Start_PendingMessageOfDeadInstance_ClaimedAndProcessed()
    {
        // Chuẩn bị: instance cũ đọc message rồi chết trước khi ACK
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_containers.RedisConnectionString + ",allowAdmin=true");
        await connection.GetServer(_containers.RedisConnectionString).FlushAllDatabasesAsync();
        var redis = connection.GetDatabase();
        await redis.StreamCreateConsumerGroupAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc, StreamPosition.Beginning, createStream: true);
        var orphan = EventEnvelope.Create(Producers.Workspace, NewMemberAdded());
        await redis.StreamAddAsync(StreamNames.Workspace,
            [new NameValueEntry("type", orphan.Type), new NameValueEntry("envelope", EventJson.Serialize(orphan))]);
        await redis.StreamReadGroupAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc, "dead-instance", StreamPosition.NewMessages);
        await Task.Delay(700);

        // Instance mới khởi động → nhận lại (XCLAIM) và xử lý
        await using var app = await TestApp.StartAsync(_containers, flushRedis: false);

        Assert.True(await Eventually.TrueAsync(() => app.Probe.MemberAdded == 1));
        Assert.True(await Eventually.TrueAsync(async () =>
            (await app.Redis.StreamPendingAsync(StreamNames.Workspace, ConsumerGroups.DiagramSvc)).PendingMessageCount == 0));
    }
}
