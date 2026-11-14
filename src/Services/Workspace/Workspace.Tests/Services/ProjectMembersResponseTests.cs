using FluentAssertions;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.Services;
using Workspace.Service.Validators;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.Services;

/// <summary>Danh sách thành viên trả kèm username + họ tên lấy từ danh bạ (UserDirectory).</summary>
public class ProjectMembersResponseTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly ProjectService _projectService;
    private readonly Guid _leaderId = Guid.NewGuid();
    private readonly Guid _notSyncedMemberId = Guid.NewGuid();

    public ProjectMembersResponseTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns(_leaderId);

        _projectService = new ProjectService(
            _projectRepo,
            new UserDirectoryRepository(_context),
            new Mock<IOutbox>().Object,
            currentUser.Object,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator());
    }

    private async Task<Project> SeedProjectAsync()
    {
        _context.UserDirectory.Add(new UserDirectory { UserId = _leaderId, Username = "student1", FullName = "Le Van C", Role = "Student" });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "CampusEats",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        // Thành viên chưa có trong danh bạ (sự kiện UserRegistered chưa tới)
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _notSyncedMemberId, Role = "Member" });

        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task GetMembersAsync_UserInDirectory_ReturnsUsernameAndFullName()
    {
        var project = await SeedProjectAsync();

        var members = await _projectService.GetMembersAsync(project.Id);

        var leader = members.Single(m => m.UserId == _leaderId);
        leader.Username.Should().Be("student1");
        leader.FullName.Should().Be("Le Van C");
        leader.Role.Should().Be("Leader");
    }

    [Fact]
    public async Task GetMembersAsync_UserNotInDirectory_ReturnsEmptyNamesWithoutError()
    {
        var project = await SeedProjectAsync();

        var members = await _projectService.GetMembersAsync(project.Id);

        var notSynced = members.Single(m => m.UserId == _notSyncedMemberId);
        notSynced.Username.Should().BeEmpty();
        notSynced.FullName.Should().BeEmpty();
        notSynced.Role.Should().Be("Member");
    }

    [Fact]
    public async Task GetProjectByIdAsync_DetailMembers_IncludeNamesFromDirectory()
    {
        var project = await SeedProjectAsync();

        var detail = await _projectService.GetProjectByIdAsync(project.Id);

        detail.Members.Single(m => m.UserId == _leaderId).FullName.Should().Be("Le Van C");
    }
}
