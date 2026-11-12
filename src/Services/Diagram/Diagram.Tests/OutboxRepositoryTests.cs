using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Repositories;

namespace UMLForge.Diagram.Tests;

public class OutboxRepositoryTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly OutboxRepository _repository;

    public OutboxRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _repository = new OutboxRepository(_dbContext);
    }

    [Fact]
    public async Task AddMessage_And_GetUnpublishedMessagesAsync_ReturnsUnpublishedOnly()
    {
        var msg1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "diagram.events",
            Type = "RenderRequested",
            Envelope = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            PublishedAt = null
        };

        var msg2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "diagram.events",
            Type = "RenderRequested",
            Envelope = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            PublishedAt = DateTime.UtcNow.AddMinutes(-9)
        };

        _repository.AddMessage(msg1);
        _repository.AddMessage(msg2);
        await _repository.SaveChangesAsync();

        var unpublished = await _repository.GetUnpublishedMessagesAsync(10);

        unpublished.Should().HaveCount(1);
        unpublished.First().Id.Should().Be(msg1.Id);
    }

    [Fact]
    public async Task MarkPublishedAsync_SetsPublishedAt_And_IncrementsAttempts()
    {
        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "diagram.events",
            Type = "RenderRequested",
            Envelope = "{}",
            CreatedAt = DateTime.UtcNow,
            Attempts = 0,
            PublishedAt = null
        };

        _repository.AddMessage(msg);
        await _repository.SaveChangesAsync();

        await _repository.MarkPublishedAsync(msg.Id);
        await _repository.SaveChangesAsync();

        var inDb = await _dbContext.OutboxMessages.FindAsync(msg.Id);
        inDb.Should().NotBeNull();
        inDb!.PublishedAt.Should().NotBeNull();
        inDb.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailureAsync_IncrementsAttempts_And_SetsLastError()
    {
        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Stream = "diagram.events",
            Type = "RenderRequested",
            Envelope = "{}",
            CreatedAt = DateTime.UtcNow,
            Attempts = 1,
            PublishedAt = null
        };

        _repository.AddMessage(msg);
        await _repository.SaveChangesAsync();

        await _repository.RecordFailureAsync(msg.Id, "Redis connection timed out");
        await _repository.SaveChangesAsync();

        var inDb = await _dbContext.OutboxMessages.FindAsync(msg.Id);
        inDb.Should().NotBeNull();
        inDb!.Attempts.Should().Be(2);
        inDb.LastError.Should().Be("Redis connection timed out");
        inDb.PublishedAt.Should().BeNull();
    }
}
