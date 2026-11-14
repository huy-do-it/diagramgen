using FluentAssertions;
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
public class BR08_LeaderOnlyMemberManagementTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly InvitationRepository _invitationRepo;
    private readonly UserDirectoryRepository _userDirectoryRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly ProjectService _projectService;
    private readonly InvitationService _invitationService;

    private readonly Guid _leaderId = Guid.NewGuid();
    private readonly Guid _regularMemberId = Guid.NewGuid();

    public BR08_LeaderOnlyMemberManagementTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _invitationRepo = new InvitationRepository(_context);
        _userDirectoryRepo = new UserDirectoryRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

        _projectService = new ProjectService(
            _projectRepo,
            _userDirectoryRepo,
            _outboxMock.Object,
            _currentUserMock.Object,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator()
        );

        _invitationService = new InvitationService(
            _invitationRepo,
            _projectRepo,
            _userDirectoryRepo,
            _outboxMock.Object,
            _currentUserMock.Object,
            new InviteMemberRequestValidator()
        );
    }

    private async Task<Project> SeedProjectWithLeaderAndMemberAsync()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Team Workspace",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _regularMemberId, Role = "Member" });

        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();
        return project;
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task UpdateProject_NonLeader_Throws403ForbiddenWithBR08()
    {
        // Arrange
        var project = await SeedProjectWithLeaderAndMemberAsync();
        _currentUserMock.Setup(u => u.UserId).Returns(_regularMemberId);

        var request = new UpdateProjectRequest("Renamed by Member", null);

        // Act
        var act = async () => await _projectService.UpdateProjectAsync(project.Id, request, Convert.ToBase64String(project.RowVersion));

        // Assert
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Code.Should().Be("BR-08");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task DeleteProject_NonLeader_Throws403ForbiddenWithBR08()
    {
        // Arrange
        var project = await SeedProjectWithLeaderAndMemberAsync();
        _currentUserMock.Setup(u => u.UserId).Returns(_regularMemberId);

        // Act
        var act = async () => await _projectService.DeleteProjectAsync(project.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Code.Should().Be("BR-08");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task RemoveMember_NonLeader_Throws403ForbiddenWithBR08()
    {
        // Arrange
        var project = await SeedProjectWithLeaderAndMemberAsync();
        var anotherMemberId = Guid.NewGuid();
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = anotherMemberId, Role = "Member" });
        await _context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(_regularMemberId);

        // Act
        var act = async () => await _projectService.RemoveMemberAsync(project.Id, anotherMemberId);

        // Assert
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Code.Should().Be("BR-08");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task RemoveMember_LeaderAttemptsToRemoveLeader_Throws403ForbiddenWithBR08()
    {
        // Arrange: Leader calling to remove self (Leader)
        var project = await SeedProjectWithLeaderAndMemberAsync();
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

        // Act
        var act = async () => await _projectService.RemoveMemberAsync(project.Id, _leaderId);

        // Assert
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Code.Should().Be("BR-08");
        ex.Which.Status.Should().Be(403);
        ex.Which.Message.Should().Contain("Không thể xóa Leader");
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task RemoveMember_LeaderRemovesRegularMember_SucceedsAndStagesMemberRemovedEvent()
    {
        // Arrange
        var project = await SeedProjectWithLeaderAndMemberAsync();
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

        // Act
        await _projectService.RemoveMemberAsync(project.Id, _regularMemberId);

        // Assert
        var members = await _projectRepo.GetMembersAsync(project.Id);
        members.Should().NotContain(m => m.UserId == _regularMemberId);

        _outboxMock.Verify(o => o.Add(
            StreamNames.Workspace,
            It.Is<MemberRemoved>(e =>
                e.ProjectId == project.Id &&
                e.UserId == _regularMemberId &&
                e.ActorUserId == _leaderId)),
            Times.Once);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task InviteMember_NonLeader_Throws403ForbiddenWithBR08()
    {
        // Arrange
        var project = await SeedProjectWithLeaderAndMemberAsync();
        _currentUserMock.Setup(u => u.UserId).Returns(_regularMemberId);

        var request = new InviteMemberRequest("someone");

        // Act
        var act = async () => await _invitationService.InviteMemberAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.Code.Should().Be("BR-08");
        ex.Which.Status.Should().Be(403);
    }
}
