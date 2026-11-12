using FluentAssertions;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.DTOs;
using Workspace.Service.Services;
using Workspace.Service.Validators;
using Workspace.Tests.Helpers;

namespace Workspace.Tests.BusinessRules;

[Trait("Trait", "BR")]
public class BR07_MaxFiveMembersTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly InvitationRepository _invitationRepo;
    private readonly UserDirectoryRepository _userDirectoryRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly InvitationService _invitationService;
    private readonly Guid _leaderId = Guid.NewGuid();

    public BR07_MaxFiveMembersTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _invitationRepo = new InvitationRepository(_context);
        _userDirectoryRepo = new UserDirectoryRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);
        _currentUserMock.Setup(u => u.Username).Returns("leader");
        _currentUserMock.Setup(u => u.Role).Returns("Student");

        _invitationService = new InvitationService(
            _invitationRepo,
            _projectRepo,
            _userDirectoryRepo,
            _outboxMock.Object,
            _currentUserMock.Object,
            new InviteMemberRequestValidator()
        );
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task InviteMember_WhenProjectHas5Members_Throws409ConflictWithBR07()
    {
        // Arrange: Project with 5 student members (1 Leader + 4 Members)
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Full Team Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        for (int i = 1; i <= 4; i++)
        {
            project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = Guid.NewGuid(), Role = "Member" });
        }
        await _projectRepo.AddAsync(project);

        var candidate = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "candidate6",
            FullName = "Candidate Six",
            Role = "Student",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(candidate);
        await _context.SaveChangesAsync();

        var request = new InviteMemberRequest("candidate6");

        // Act
        var act = async () => await _invitationService.InviteMemberAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-07");
        ex.Which.Status.Should().Be(409);
        ex.Which.Message.Should().Contain("tối đa 5 thành viên");
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task InviteMember_WhenProjectHas4MembersAnd1PendingInvitation_Throws409ConflictWithBR07()
    {
        // Arrange: 4 members + 1 pending invitation = 5 slots taken
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Nearly Full Team",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        for (int i = 1; i <= 3; i++)
        {
            project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = Guid.NewGuid(), Role = "Member" });
        }
        await _projectRepo.AddAsync(project);

        var pendingInv = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            InviteeId = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await _invitationRepo.AddAsync(pendingInv);

        var candidate = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "candidate_new",
            FullName = "Candidate New",
            Role = "Student",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(candidate);
        await _context.SaveChangesAsync();

        var request = new InviteMemberRequest("candidate_new");

        // Act
        var act = async () => await _invitationService.InviteMemberAsync(project.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-07");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task InviteMember_LecturerDoesNotCountTowards5MemberLimit_AllowsInvitation()
    {
        // Arrange: 4 student members + 1 Lecturer = total 5 members in table, but student count is 4.
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Team with Lecturer",
            OwnerId = _leaderId,
            LecturerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        for (int i = 1; i <= 3; i++)
        {
            project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = Guid.NewGuid(), Role = "Member" });
        }
        // Lecturer as a member
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = project.LecturerId.Value, Role = "Lecturer" });
        await _projectRepo.AddAsync(project);

        var candidate = new UserDirectory
        {
            UserId = Guid.NewGuid(),
            Username = "student5",
            FullName = "Fifth Student",
            Role = "Student",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(candidate);
        await _context.SaveChangesAsync();

        var request = new InviteMemberRequest("student5");

        // Act
        var result = await _invitationService.InviteMemberAsync(project.Id, request);

        // Assert: Succeeds because Lecturer is excluded
        result.Should().NotBeNull();
        result.Status.Should().Be("Pending");
        result.InviteeId.Should().Be(candidate.UserId);
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task AcceptInvitation_WhenProjectCapacityAlreadyReached_Throws409ConflictWithBR07()
    {
        // Arrange: 5 members in project
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Full Team Acceptance",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        for (int i = 1; i <= 4; i++)
        {
            project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = Guid.NewGuid(), Role = "Member" });
        }
        await _projectRepo.AddAsync(project);

        var inviteeId = Guid.NewGuid();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            InviteeId = inviteeId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Project = project
        };
        await _invitationRepo.AddAsync(invitation);
        await _context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(inviteeId);

        // Act
        var act = async () => await _invitationService.AcceptInvitationAsync(invitation.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-07");
        ex.Which.Status.Should().Be(409);
    }
}
