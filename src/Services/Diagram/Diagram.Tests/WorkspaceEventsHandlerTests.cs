using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using UMLForge.Diagram.Service.Events;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class WorkspaceEventsHandlerTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly MemberAccessRepository _memberAccessRepo;
    private readonly DiagramRepository _diagramRepo;
    private readonly InboxRepository _inboxRepo;
    private readonly WorkspaceEventsHandler _handler;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public WorkspaceEventsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _memberAccessRepo = new MemberAccessRepository(_dbContext);
        _diagramRepo = new DiagramRepository(_dbContext);
        _inboxRepo = new InboxRepository(_dbContext);

        _handler = new WorkspaceEventsHandler(_memberAccessRepo, _diagramRepo, _inboxRepo);
    }

    [Fact]
    public async Task HandleProjectCreated_CreatesLeaderAccess_And_RecordsInboxMessage()
    {
        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<ProjectCreated>(
            eventId,
            "ProjectCreated",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new ProjectCreated(_testProjectId, "CampusEats", _testUserId, _testUserId)
        );

        var handled = await _handler.HandleProjectCreatedAsync(envelope);

        handled.Should().BeTrue();
        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().NotBeNull();
        access!.Role.Should().Be(ProjectRole.Leader);

        var processed = await _inboxRepo.HasBeenProcessedAsync(eventId, "diagram-svc");
        processed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleProjectCreated_WhenDuplicate_IsIdempotent()
    {
        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<ProjectCreated>(
            eventId,
            "ProjectCreated",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new ProjectCreated(_testProjectId, "CampusEats", _testUserId, _testUserId)
        );

        await _handler.HandleProjectCreatedAsync(envelope);
        var secondRun = await _handler.HandleProjectCreatedAsync(envelope);

        secondRun.Should().BeFalse();
    }

    [Fact]
    public async Task HandleMemberAdded_AddsOrUpdatesMemberAccess()
    {
        var envelope = new EventEnvelope<MemberAdded>(
            Guid.NewGuid(),
            "MemberAdded",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new MemberAdded(_testProjectId, "CampusEats", _testUserId, ProjectRole.Member, Guid.NewGuid())
        );

        await _handler.HandleMemberAddedAsync(envelope);

        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().NotBeNull();
        access!.Role.Should().Be(ProjectRole.Member);
    }

    [Fact]
    public async Task HandleMemberRemoved_RemovesAccess()
    {
        await _memberAccessRepo.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<MemberRemoved>(
            Guid.NewGuid(),
            "MemberRemoved",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new MemberRemoved(_testProjectId, _testUserId, Guid.NewGuid())
        );

        await _handler.HandleMemberRemovedAsync(envelope);

        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().BeNull();
    }

    [Fact]
    public async Task HandleLecturerAssigned_ReplacesPreviousLecturer()
    {
        var oldLecturerId = Guid.NewGuid();
        var newLecturerId = Guid.NewGuid();

        await _memberAccessRepo.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = oldLecturerId,
            Role = ProjectRole.Lecturer
        });
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<LecturerAssigned>(
            Guid.NewGuid(),
            "LecturerAssigned",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new LecturerAssigned(_testProjectId, "CampusEats", newLecturerId, oldLecturerId, Guid.NewGuid())
        );

        await _handler.HandleLecturerAssignedAsync(envelope);

        var oldAccess = await _memberAccessRepo.GetAccessAsync(_testProjectId, oldLecturerId);
        oldAccess.Should().BeNull();

        var newAccess = await _memberAccessRepo.GetAccessAsync(_testProjectId, newLecturerId);
        newAccess.Should().NotBeNull();
        newAccess!.Role.Should().Be(ProjectRole.Lecturer);
    }

    [Fact]
    public async Task HandleProjectDeleted_RemovesAccess_And_SoftDeletesDiagrams()
    {
        await _memberAccessRepo.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Leader
        });

        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = "{}",
            Status = DiagramStatus.Draft,
            IsDeleted = false
        };
        await _diagramRepo.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var envelope = new EventEnvelope<ProjectDeleted>(
            Guid.NewGuid(),
            "ProjectDeleted",
            1,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "workspace",
            new ProjectDeleted(_testProjectId, _testUserId)
        );

        await _handler.HandleProjectDeletedAsync(envelope);

        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().BeNull();

        var inDb = await _dbContext.Diagrams.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == diagram.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }
}
