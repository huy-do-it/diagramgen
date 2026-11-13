using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class RenderEventsHandlerTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepo;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _testDiagramId = Guid.NewGuid();

    public RenderEventsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _diagramRepo = new DiagramRepository(_dbContext);
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

        var handler = new RenderStartedHandler(_diagramRepo);
        var envelope = EventEnvelope.Create(
            "render",
            new RenderStarted(Guid.NewGuid(), _testDiagramId, _testProjectId, 1));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

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

        var handler = new RenderCompletedHandler(_diagramRepo);
        var envelope = EventEnvelope.Create(
            "render",
            new RenderCompleted(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Use Case 1", "<svg>rendered</svg>", "@startuml\n@enduml", 150, Guid.NewGuid()));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

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

        var handler = new RenderFailedHandler(_diagramRepo);
        var envelope = EventEnvelope.Create(
            "render",
            new RenderFailed(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Use Case 1", "PlantUML timeout 30s", Guid.NewGuid()));

        // Act
        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
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

        var handler = new RenderCompletedHandler(_diagramRepo);
        var envelope = EventEnvelope.Create(
            "render",
            new RenderCompleted(Guid.NewGuid(), _testDiagramId, _testProjectId, 2, "Old v2", "<svg>v2</svg>", "@startuml", 100, Guid.NewGuid()));

        // Act
        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert: Không ghi đè v3 bằng v2
        var inDb = await _diagramRepo.GetByIdAsync(_testDiagramId);
        inDb!.Status.Should().Be(DiagramStatus.Draft);
        inDb.SvgXml.Should().Be("<svg>v3_draft</svg>");
    }
}
