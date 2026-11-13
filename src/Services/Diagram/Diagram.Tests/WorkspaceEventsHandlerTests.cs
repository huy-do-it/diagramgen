using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class WorkspaceEventsHandlerTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly MemberAccessRepository _memberAccessRepo;
    private readonly DiagramRepository _diagramRepo;

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
    }

    [Fact]
    public async Task HandleProjectCreated_CreatesLeaderAccess()
    {
        var handler = new ProjectCreatedHandler(_memberAccessRepo);
        var envelope = EventEnvelope.Create(
            "workspace",
            new ProjectCreated(_testProjectId, "CampusEats", _testUserId, _testUserId));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().NotBeNull();
        access!.Role.Should().Be(ProjectRole.Leader);
    }

    [Fact]
    public async Task HandleMemberAdded_AddsOrUpdatesMemberAccess()
    {
        var handler = new MemberAddedHandler(_memberAccessRepo);
        var envelope = EventEnvelope.Create(
            "workspace",
            new MemberAdded(_testProjectId, "CampusEats", _testUserId, ProjectRole.Member, Guid.NewGuid()));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

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

        var handler = new MemberRemovedHandler(_memberAccessRepo);
        var envelope = EventEnvelope.Create(
            "workspace",
            new MemberRemoved(_testProjectId, _testUserId, Guid.NewGuid()));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

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

        var handler = new LecturerAssignedHandler(_memberAccessRepo);
        var envelope = EventEnvelope.Create(
            "workspace",
            new LecturerAssigned(_testProjectId, "CampusEats", newLecturerId, oldLecturerId, Guid.NewGuid()));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

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

        var handler = new ProjectDeletedHandler(_memberAccessRepo, _diagramRepo);
        var envelope = EventEnvelope.Create(
            "workspace",
            new ProjectDeleted(_testProjectId, _testUserId));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var access = await _memberAccessRepo.GetAccessAsync(_testProjectId, _testUserId);
        access.Should().BeNull();

        var inDb = await _dbContext.Diagrams.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == diagram.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }
}
