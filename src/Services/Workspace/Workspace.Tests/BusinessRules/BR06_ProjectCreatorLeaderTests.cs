using FluentAssertions;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Data;
using Workspace.Repositories.Repositories;
using Workspace.Service.DTOs;
using Workspace.Service.Services;
using Workspace.Service.Validators;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR06_ProjectCreatorLeaderTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly ProjectService _projectService;
    private readonly Guid _creatorId = Guid.NewGuid();

    public BR06_ProjectCreatorLeaderTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_creatorId);
        _currentUserMock.Setup(u => u.Username).Returns("creator_student");
        _currentUserMock.Setup(u => u.Role).Returns("Student");

        _projectService = new ProjectService(
            _projectRepo,
            _outboxMock.Object,
            _currentUserMock.Object,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator()
        );
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task CreateProject_CreatorAutomaticallyBecomesLeader()
    {
        // Arrange
        var request = new CreateProjectRequest("PRN232 Capstone", "Hệ thống vẽ UML phân tán");

        // Act
        var result = await _projectService.CreateProjectAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("PRN232 Capstone");
        result.OwnerId.Should().Be(_creatorId);
        result.Members.Should().HaveCount(1);

        var member = result.Members.Single();
        member.UserId.Should().Be(_creatorId);
        member.Role.Should().Be("Leader");

        // Verify entity in DbContext
        var savedProject = await _projectRepo.GetByIdWithMembersAsync(result.Id);
        savedProject.Should().NotBeNull();
        savedProject!.OwnerId.Should().Be(_creatorId);
        savedProject.Members.Should().ContainSingle(m => m.UserId == _creatorId && m.Role == "Leader");
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task CreateProject_StagesProjectCreatedEventInOutbox()
    {
        // Arrange
        var request = new CreateProjectRequest("Project Alpha", "Description Alpha");

        // Act
        var result = await _projectService.CreateProjectAsync(request);

        // Assert: verify Outbox was called with StreamNames.Workspace and ProjectCreated payload
        _outboxMock.Verify(o => o.Add(
            StreamNames.Workspace,
            It.Is<ProjectCreated>(e =>
                e.ProjectId == result.Id &&
                e.Name == "Project Alpha" &&
                e.OwnerId == _creatorId &&
                e.ActorUserId == _creatorId)),
            Times.Once);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task CreateProject_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(false);
        var request = new CreateProjectRequest("Unauthorized Project", null);

        // Act
        var act = async () => await _projectService.CreateProjectAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
