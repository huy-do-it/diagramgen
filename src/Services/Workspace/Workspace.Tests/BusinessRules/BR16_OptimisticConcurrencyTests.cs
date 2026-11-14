using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.DTOs;
using Workspace.Service.Services;
using Workspace.Service.Validators;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR16_OptimisticConcurrencyTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly ProjectService _projectService;
    private readonly Guid _leaderId = Guid.NewGuid();

    public BR16_OptimisticConcurrencyTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

        _projectService = new ProjectService(
            _projectRepo,
            new UserDirectoryRepository(_context),
            _outboxMock.Object,
            _currentUserMock.Object,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator()
        );
    }

    [Fact]
    public async Task UpdateProject_MissingIfMatchHeader_Throws409ConflictWithConcurrencyConflict()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Concurrency Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();

        var request = new UpdateProjectRequest("Updated Name", null);

        // Act: missing ifMatch (null)
        var act = async () => await _projectService.UpdateProjectAsync(project.Id, request, null);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("CONCURRENCY_CONFLICT");
        ex.Which.Status.Should().Be(409);
        ex.Which.Message.Should().Contain("If-Match là bắt buộc");
    }

    [Fact]
    public async Task UpdateProject_MismatchedIfMatchHeader_Throws409ConflictWithConcurrencyConflict()
    {
        // Arrange
        var actualRowVersion = Guid.NewGuid().ToByteArray();
        var staleRowVersion = Guid.NewGuid().ToByteArray();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Concurrency Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = actualRowVersion
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();

        var request = new UpdateProjectRequest("Updated Name", null);
        var staleETag = $"\"{Convert.ToBase64String(staleRowVersion)}\"";

        // Act: stale ETag
        var act = async () => await _projectService.UpdateProjectAsync(project.Id, request, staleETag);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("CONCURRENCY_CONFLICT");
        ex.Which.Status.Should().Be(409);
        ex.Which.Message.Should().Contain("thay đổi bởi người khác");
    }

    [Fact]
    public async Task UpdateProject_MatchingIfMatchHeader_SucceedsAndGeneratesNewRowVersion()
    {
        // Arrange
        var initialRowVersion = Guid.NewGuid().ToByteArray();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = initialRowVersion
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();

        var request = new UpdateProjectRequest("Updated Name", "New Description");
        var validETag = $"\"{Convert.ToBase64String(initialRowVersion)}\"";

        // Act
        var result = await _projectService.UpdateProjectAsync(project.Id, request, validETag);

        // Assert
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("New Description");
        result.RowVersion.Should().NotEqual(initialRowVersion);
    }

    [Fact]
    public async Task DeleteProject_SoftDeletesAndStagesProjectDeletedEvent()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "To Be Deleted",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        await _projectService.DeleteProjectAsync(project.Id);

        // Assert
        var savedProject = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == project.Id);
        savedProject!.IsDeleted.Should().BeTrue();

        _outboxMock.Verify(o => o.Add(
            StreamNames.Workspace,
            It.Is<ProjectDeleted>(e =>
                e.ProjectId == project.Id &&
                e.ActorUserId == _leaderId)),
            Times.Once);
    }
}
