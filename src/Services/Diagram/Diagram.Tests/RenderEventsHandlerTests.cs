using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using UMLForge.Diagram.Service.Events;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class RenderEventsHandlerTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepo;
    private readonly InboxRepository _inboxRepo;
    private readonly RenderEventsHandler _handler;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _testDiagramId = Guid.NewGuid();

    public RenderEventsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _diagramRepo = new DiagramRepository(_dbContext);
        _inboxRepo = new InboxRepository(_dbContext);

        _handler = new RenderEventsHandler(_diagramRepo, _inboxRepo);
    }

    [Fact]
    public async Task HandleRenderStarted_SetsStatusToRendering()
    {
        var diagram = new Repositories.Entities.Diagram
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Pending,
            CurrentVersion = 1
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<RenderStarted>(
            Guid.NewGuid(),
            "RenderStarted",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "render",
            new RenderStarted(Guid.NewGuid(), _testDiagramId, _testProjectId, 1)
        );

        var handled = await _handler.HandleRenderStartedAsync(envelope);

        handled.Should().BeTrue();
        var inDb = await _diagramRepo.GetByIdAsync(_testDiagramId);
        inDb!.Status.Should().Be(DiagramStatus.Rendering);
    }

    [Fact]
    public async Task HandleRenderCompleted_UpdatesSvgAndStatusToRendered()
    {
        var diagram = new Repositories.Entities.Diagram
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Rendering,
            CurrentVersion = 2
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<RenderCompleted>(
            Guid.NewGuid(),
            "RenderCompleted",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "render",
            new RenderCompleted(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Use Case 1", "<svg>rendered</svg>", "@startuml\n@enduml", 150, Guid.NewGuid())
        );

        var handled = await _handler.HandleRenderCompletedAsync(envelope);

        handled.Should().BeTrue();
        var inDb = await _diagramRepo.GetByIdAsync(_testDiagramId);
        inDb!.Status.Should().Be(DiagramStatus.Rendered);
        inDb.SvgXml.Should().Be("<svg>rendered</svg>");
        inDb.PlantUml.Should().Be("@startuml\n@enduml");
        inDb.RenderedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("BR", "BR-28")]
    public async Task HandleRenderFailed_SetsStatusToFailed_And_PreservesExistingSvgXml()
    {
        // Arrange (BR-28: Render lỗi giữ lại SVG cũ)
        var diagram = new Repositories.Entities.Diagram
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Rendering,
            CurrentVersion = 2,
            SvgXml = "<svg>previous_valid_version</svg>"
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<RenderFailed>(
            Guid.NewGuid(),
            "RenderFailed",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "render",
            new RenderFailed(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Use Case 1", "PlantUML timeout 30s", Guid.NewGuid())
        );

        // Act
        var handled = await _handler.HandleRenderFailedAsync(envelope);

        // Assert
        handled.Should().BeTrue();
        var inDb = await _diagramRepo.GetByIdAsync(_testDiagramId);
        inDb!.Status.Should().Be(DiagramStatus.Failed);
        inDb.LastError.Should().Be("PlantUML timeout 30s");
        inDb.SvgXml.Should().Be("<svg>previous_valid_version</svg>"); // BR-28: KHÔNG bị xóa
    }

    [Fact]
    public async Task HandleRenderCompleted_WhenVersionIsOlderThanCurrent_IgnoresUpdate()
    {
        // Arrange: Diagram đã sửa lên version 3 trong khi job version 2 hoàn thành
        var diagram = new Repositories.Entities.Diagram
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Draft,
            CurrentVersion = 3,
            SvgXml = "<svg>v3_draft</svg>"
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<RenderCompleted>(
            Guid.NewGuid(),
            "RenderCompleted",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "render",
            new RenderCompleted(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Old v2", "<svg>v2</svg>", "@startuml", 100, Guid.NewGuid())
        );

        // Act
        await _handler.HandleRenderCompletedAsync(envelope);

        // Assert: Không ghi đè v3 bằng v2
        var inDb = await _diagramRepo.GetByIdAsync(_testDiagramId);
        inDb!.Status.Should().Be(DiagramStatus.Draft);
        inDb.SvgXml.Should().Be("<svg>v3_draft</svg>");
    }

    [Fact]
    public async Task HandleRenderCompleted_WhenDuplicateEvent_IsIdempotent()
    {
        var diagram = new Repositories.Entities.Diagram
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Rendering,
            CurrentVersion = 1
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<RenderCompleted>(
            eventId,
            "RenderCompleted",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "render",
            new RenderCompleted(Guid.NewGuid(), _testDiagramId, _testProjectId, 1, "v1", "<svg/>", "@puml", 50, Guid.NewGuid())
        );

        var first = await _handler.HandleRenderCompletedAsync(envelope);
        var second = await _handler.HandleRenderCompletedAsync(envelope);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }
}
