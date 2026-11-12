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
public class BR10_LecturerRoleValidationTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly UserDirectoryRepository _userDirectoryRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly UserDirectoryService _directoryService;
    private readonly Guid _leaderId = Guid.NewGuid();

    public BR10_LecturerRoleValidationTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _userDirectoryRepo = new UserDirectoryRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

        _directoryService = new UserDirectoryService(
            _userDirectoryRepo,
            _projectRepo,
            _outboxMock.Object,
            _currentUserMock.Object,
            new AssignLecturerRequestValidator()
        );
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task AssignLecturer_WhenUserHasStudentRole_Throws400BadRequestWithBR10()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Lecturer Test Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var studentUser = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "student_guy",
            FullName = "Student Guy",
            Role = "Student",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(studentUser);
        await _context.SaveChangesAsync();

        var request = new AssignLecturerRequest(studentUser.UserId);

        // Act
        var act = async () => await _directoryService.AssignLecturerAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-10");
        ex.Which.Status.Should().Be(400);
        ex.Which.Message.Should().Contain("vai trò Lecturer");
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task AssignLecturer_WhenUserHasAdminRole_Throws400BadRequestWithBR10()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Admin Lecturer Test",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var adminUser = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "admin_guy",
            FullName = "Admin Guy",
            Role = "Admin",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(adminUser);
        await _context.SaveChangesAsync();

        var request = new AssignLecturerRequest(adminUser.UserId);

        // Act
        var act = async () => await _directoryService.AssignLecturerAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-10");
        ex.Which.Status.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task AssignLecturer_WhenLecturerIsLocked_Throws400BadRequestWithBR03()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Locked Lecturer Test",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var lockedLecturer = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "locked_lecturer",
            FullName = "Locked Lecturer",
            Role = "Lecturer",
            IsLocked = true
        };
        await _userDirectoryRepo.AddAsync(lockedLecturer);
        await _context.SaveChangesAsync();

        var request = new AssignLecturerRequest(lockedLecturer.UserId);

        // Act
        var act = async () => await _directoryService.AssignLecturerAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-03");
        ex.Which.Status.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task AssignLecturer_WhenValidLecturer_SucceedsAndStagesLecturerAssignedEvent()
    {
        // Arrange
        var initialLecturerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Supervisor Assignment Project",
            OwnerId = _leaderId,
            LecturerId = initialLecturerId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = initialLecturerId, Role = "Lecturer" });
        await _projectRepo.AddAsync(project);

        var newLecturer = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "new_lecturer",
            FullName = "Dr. New Lecturer",
            Role = "Lecturer",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(newLecturer);
        await _context.SaveChangesAsync();

        var request = new AssignLecturerRequest(newLecturer.UserId);

        // Act
        var result = await _directoryService.AssignLecturerAsync(project.Id, request);

        // Assert
        result.LecturerId.Should().Be(newLecturer.UserId);

        var savedProject = await _projectRepo.GetByIdWithMembersAsync(project.Id);
        savedProject!.LecturerId.Should().Be(newLecturer.UserId);
        savedProject.Members.Should().ContainSingle(m => m.UserId == newLecturer.UserId && m.Role == "Lecturer");
        savedProject.Members.Should().NotContain(m => m.UserId == initialLecturerId);

        _outboxMock.Verify(o => o.Add(
            StreamNames.Workspace,
            It.Is<LecturerAssigned>(e =>
                e.ProjectId == project.Id &&
                e.ProjectName == project.Name &&
                e.LecturerId == newLecturer.UserId &&
                e.PreviousLecturerId == initialLecturerId &&
                e.ActorUserId == _leaderId)),
            Times.Once);
    }
}
