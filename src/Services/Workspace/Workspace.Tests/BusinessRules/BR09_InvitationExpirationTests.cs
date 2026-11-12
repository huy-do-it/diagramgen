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
public class BR09_InvitationExpirationTests
{
    private readonly WorkspaceDbContext _context;
    private readonly ProjectRepository _projectRepo;
    private readonly InvitationRepository _invitationRepo;
    private readonly UserDirectoryRepository _userDirectoryRepo;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly InvitationService _invitationService;

    private readonly Guid _leaderId = Guid.NewGuid();
    private readonly Guid _inviteeId = Guid.NewGuid();

    public BR09_InvitationExpirationTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _projectRepo = new ProjectRepository(_context);
        _invitationRepo = new InvitationRepository(_context);
        _userDirectoryRepo = new UserDirectoryRepository(_context);
        _outboxMock = new Mock<IOutbox>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(u => u.UserId).Returns(_leaderId);

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
    [Trait("BR", "BR-09")]
    public async Task InviteMember_SetsExpirationToExactly7Days()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Expiration Test Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var candidate = new UserDirectory
        {
            UserId = _inviteeId,
            Username = "invitee_user",
            FullName = "Invitee User",
            Role = "Student",
            IsLocked = false
        };
        await _userDirectoryRepo.AddAsync(candidate);
        await _context.SaveChangesAsync();

        var request = new InviteMemberRequest("invitee_user");

        // Act
        var result = await _invitationService.InviteMemberAsync(project.Id, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Pending");
        result.ExpiresAt.Should().BeCloseTo(result.CreatedAt.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task AcceptInvitation_WhenExpired_Throws410GoneWithBR09AndUpdatesStatusToExpired()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Expired Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var expiredInvitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            InviteeId = _inviteeId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Past expiration date
            Project = project
        };
        await _invitationRepo.AddAsync(expiredInvitation);
        await _context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(_inviteeId);

        // Act
        var act = async () => await _invitationService.AcceptInvitationAsync(expiredInvitation.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-09");
        ex.Which.Status.Should().Be(410);

        // Verify status was updated to Expired
        var savedInv = await _invitationRepo.GetByIdAsync(expiredInvitation.Id);
        savedInv!.Status.Should().Be("Expired");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task AcceptInvitation_WhenValid_AddsMemberAndStagesMemberAddedEvent()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Active Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        project.Members.Add(new ProjectMember { ProjectId = project.Id, UserId = _leaderId, Role = "Leader" });
        await _projectRepo.AddAsync(project);

        var validInvitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            InviteeId = _inviteeId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            Project = project
        };
        await _invitationRepo.AddAsync(validInvitation);
        await _context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(_inviteeId);

        // Act
        var result = await _invitationService.AcceptInvitationAsync(validInvitation.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Accepted");

        var members = await _projectRepo.GetMembersAsync(project.Id);
        members.Should().ContainSingle(m => m.UserId == _inviteeId && m.Role == "Member");

        _outboxMock.Verify(o => o.Add(
            StreamNames.Workspace,
            It.Is<MemberAdded>(e =>
                e.ProjectId == project.Id &&
                e.ProjectName == project.Name &&
                e.UserId == _inviteeId &&
                e.Role == "Member" &&
                e.ActorUserId == _inviteeId)),
            Times.Once);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task DeclineInvitation_WhenValid_SetsStatusToDeclined()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Declining Project",
            OwnerId = _leaderId,
            CreatedAt = DateTime.UtcNow
        };
        await _projectRepo.AddAsync(project);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            InviteeId = _inviteeId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Project = project
        };
        await _invitationRepo.AddAsync(invitation);
        await _context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(_inviteeId);

        // Act
        var result = await _invitationService.DeclineInvitationAsync(invitation.Id);

        // Assert
        result.Status.Should().Be("Declined");
        var savedInv = await _invitationRepo.GetByIdAsync(invitation.Id);
        savedInv!.Status.Should().Be("Declined");
    }
}
