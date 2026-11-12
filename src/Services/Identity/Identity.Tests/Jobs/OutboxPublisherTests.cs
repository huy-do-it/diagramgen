using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;
using Identity.Service.Jobs;
using Identity.Tests.Helpers;

namespace Identity.Tests.Jobs;

public class OutboxPublisherTests
{
    private readonly IdentityDbContext _context;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<ILogger<OutboxPublisher>> _loggerMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;

    public OutboxPublisherTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IdentityDbContext))).Returns(_context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _loggerMock = new Mock<ILogger<OutboxPublisher>>();

        _redisDbMock = new Mock<IDatabase>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisMock.Setup(r => r.IsConnected).Returns(true);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
    }

    [Fact]
    public async Task PublishPendingAsync_WhenRedisUnavailable_ReturnsZeroWithoutThrowing()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var publisher = new OutboxPublisher(_scopeFactoryMock.Object, _loggerMock.Object, _redisMock.Object);

        // Act
        var result = await publisher.PublishPendingAsync();

        // Assert
        result.Should().Be(0);
        _redisDbMock.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<NameValueEntry[]>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()
        ), Times.Never);
    }

    [Fact]
    public async Task PublishPendingAsync_WhenNullRedis_ReturnsZeroWithoutThrowing()
    {
        // Arrange
        var publisher = new OutboxPublisher(_scopeFactoryMock.Object, _loggerMock.Object, redis: null);

        // Act
        var result = await publisher.PublishPendingAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task PublishPendingAsync_WhenPendingMessagesExist_PublishesToRedisStreamAndSetsPublishedAt()
    {
        // Arrange
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "identity.events",
            Type = "UserRegistered",
            Envelope = "{\"userId\":\"12345\"}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            PublishedAt = null
        };
        _context.OutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync();

        _redisDbMock
            .Setup(d => d.StreamAddAsync(
                It.Is<RedisKey>(k => k == "identity.events"),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.Is<int?>(m => m == 10000),
                It.Is<bool>(u => u == true),
                It.IsAny<CommandFlags>()
            ))
            .ReturnsAsync(new RedisValue("1700000000000-0"));

        var publisher = new OutboxPublisher(_scopeFactoryMock.Object, _loggerMock.Object, _redisMock.Object);

        // Act
        var count = await publisher.PublishPendingAsync();

        // Assert
        count.Should().Be(1);

        var updated = await _context.OutboxMessages.FindAsync(outboxMessage.Id);
        updated!.PublishedAt.Should().NotBeNull();
        updated.LastError.Should().BeNull();

        _redisDbMock.Verify(d => d.StreamAddAsync(
            It.Is<RedisKey>(k => k == "identity.events"),
            It.Is<NameValueEntry[]>(entries =>
                entries.Any(e => e.Name == "type" && e.Value == "UserRegistered") &&
                entries.Any(e => e.Name == "envelope" && e.Value == "{\"userId\":\"12345\"}")
            ),
            It.IsAny<RedisValue?>(),
            It.Is<int?>(m => m == 10000),
            It.Is<bool>(u => u == true),
            It.IsAny<CommandFlags>()
        ), Times.Once);
    }

    [Fact]
    public async Task PublishPendingAsync_WhenRedisThrowsException_IncrementsAttemptsRecordsLastErrorAndHaltsBatch()
    {
        // Arrange
        var msg1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "identity.events",
            Type = "UserRegistered",
            Envelope = "{\"userId\":\"1\"}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            PublishedAt = null,
            Attempts = 0
        };
        var msg2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "identity.events",
            Type = "UserRegistered",
            Envelope = "{\"userId\":\"2\"}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            PublishedAt = null,
            Attempts = 0
        };
        _context.OutboxMessages.AddRange(msg1, msg2);
        await _context.SaveChangesAsync();

        _redisDbMock
            .Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()
            ))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis offline"));

        var publisher = new OutboxPublisher(_scopeFactoryMock.Object, _loggerMock.Object, _redisMock.Object);

        // Act
        var count = await publisher.PublishPendingAsync();

        // Assert
        count.Should().Be(0);

        var updatedMsg1 = await _context.OutboxMessages.FindAsync(msg1.Id);
        updatedMsg1!.PublishedAt.Should().BeNull();
        updatedMsg1.Attempts.Should().Be(1);
        updatedMsg1.LastError.Should().Contain("Redis offline");

        // Second message should not be attempted in this cycle to maintain FIFO
        var updatedMsg2 = await _context.OutboxMessages.FindAsync(msg2.Id);
        updatedMsg2!.PublishedAt.Should().BeNull();
        updatedMsg2.Attempts.Should().Be(0);
    }
}
