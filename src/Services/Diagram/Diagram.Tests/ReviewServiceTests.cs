using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;
using Xunit;
using DiagramEntity = UMLForge.Diagram.Repositories.Entities.Diagram;

namespace UMLForge.Diagram.Tests;

public class ReviewServiceTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly IReviewRepository _reviewRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly ReviewService _service;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _leaderUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _lecturerUserId = Guid.NewGuid();

    public ReviewServiceTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _reviewRepository = new ReviewRepository(_dbContext);
        _diagramRepository = new DiagramRepository(_dbContext);
        _memberAccessRepository = new MemberAccessRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUser>();
        _outboxMock = new Mock<IOutbox>();

        _currentUserMock.Setup(u => u.UserId).Returns(_leaderUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("leader1");

        _service = new ReviewService(
            _reviewRepository,
            _diagramRepository,
            _memberAccessRepository,
            _currentUserMock.Object,
            _outboxMock.Object);
    }

    [Fact]
    [Trait("BR", "BR-29")]
    public async Task SubmitReview_NonLeaderUser_ThrowsForbidden()
    {
        // Setup: user is Member, not Leader
        _currentUserMock.Setup(u => u.UserId).Returns(_memberUserId);
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _memberUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateReviewRequest { ProjectId = _testProjectId };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SubmitReviewAsync(request));
        Assert.Equal("BR-29", ex.Code);
        Assert.Equal(403, ex.Status);
    }

    [Fact]
    [Trait("BR", "BR-29")]
    public async Task SubmitReview_ProjectWithNoDiagrams_ThrowsBadRequest()
    {
        // Setup: Leader access, but 0 diagrams
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _leaderUserId,
            Role = ProjectRole.Leader
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateReviewRequest { ProjectId = _testProjectId };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SubmitReviewAsync(request));
        Assert.Equal("BR-29", ex.Code);
        Assert.Equal(400, ex.Status);
        Assert.Contains("no diagrams", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("BR", "BR-29")]
    public async Task SubmitReview_DiagramsNotRendered_ThrowsBadRequestWithUnrenderedList()
    {
        // Setup: Leader access, 2 diagrams (1 Rendered, 1 Draft)
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _leaderUserId,
            Role = ProjectRole.Leader
        });
        await _diagramRepository.AddAsync(new DiagramEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Title = "Rendered Diagram",
            Type = DiagramType.UseCase,
            Status = DiagramStatus.Rendered,
            ModelJson = "{}"
        });
        await _diagramRepository.AddAsync(new DiagramEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Title = "Draft Diagram",
            Type = DiagramType.Sequence,
            Status = DiagramStatus.Draft,
            ModelJson = "{}"
        });
        await _diagramRepository.SaveChangesAsync();

        var request = new CreateReviewRequest { ProjectId = _testProjectId };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SubmitReviewAsync(request));
        Assert.Equal("BR-29", ex.Code);
        Assert.Equal(400, ex.Status);
        Assert.NotNull(ex.Errors);
        Assert.True(ex.Errors.ContainsKey("unrenderedDiagrams"));
    }

    [Fact]
    [Trait("BR", "BR-29")]
    public async Task SubmitReview_AllDiagramsRenderedAndUserIsLeader_CreatesReviewWithSnapshot()
    {
        // Setup: Leader access, 2 Rendered diagrams
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _leaderUserId,
            Role = ProjectRole.Leader
        });
        await _diagramRepository.AddAsync(new DiagramEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Title = "Diagram 1",
            Type = DiagramType.UseCase,
            Status = DiagramStatus.Rendered,
            ModelJson = "{}"
        });
        await _diagramRepository.AddAsync(new DiagramEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Title = "Diagram 2",
            Type = DiagramType.Sequence,
            Status = DiagramStatus.Rendered,
            ModelJson = "{}"
        });
        await _diagramRepository.SaveChangesAsync();

        var request = new CreateReviewRequest { ProjectId = _testProjectId };

        var result = await _service.SubmitReviewAsync(request);

        Assert.NotNull(result);
        Assert.Equal(ReviewStatus.Submitted, result.Status);
        Assert.Equal(_testProjectId, result.ProjectId);
        Assert.Equal(_leaderUserId, result.SubmittedBy);
        Assert.Equal(2, result.DiagramCount);
        Assert.Equal(2, result.Diagrams.Count);
    }

    [Fact]
    [Trait("BR", "BR-30")]
    public async Task ApproveReview_NonLecturerUser_ThrowsForbidden()
    {
        // Setup: user is Student/Member
        _currentUserMock.Setup(u => u.UserId).Returns(_memberUserId);
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _memberUserId,
            Role = ProjectRole.Member
        });

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Submitted,
            SubmittedBy = _leaderUserId,
            SnapshotJson = "[]"
        };
        await _reviewRepository.AddAsync(review);
        await _reviewRepository.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ApproveReviewAsync(review.Id));
        Assert.Equal("BR-30", ex.Code);
        Assert.Equal(403, ex.Status);
    }

    [Fact]
    [Trait("BR", "BR-30")]
    public async Task ApproveReview_AssignedLecturer_ApprovesReview()
    {
        // Setup: user is Lecturer
        _currentUserMock.Setup(u => u.UserId).Returns(_lecturerUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Lecturer");
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _lecturerUserId,
            Role = ProjectRole.Lecturer
        });

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Submitted,
            SubmittedBy = _leaderUserId,
            SnapshotJson = "[]"
        };
        await _reviewRepository.AddAsync(review);
        await _reviewRepository.SaveChangesAsync();

        var result = await _service.ApproveReviewAsync(review.Id, new ReviewDecisionRequest { Note = "Well done!" });

        Assert.Equal(ReviewStatus.Approved, result.Status);
        Assert.Equal(_lecturerUserId, result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal("Well done!", result.Note);
    }

    [Fact]
    [Trait("BR", "BR-30")]
    public async Task RequestChanges_AssignedLecturer_SetsChangesRequested()
    {
        // Setup: user is Lecturer
        _currentUserMock.Setup(u => u.UserId).Returns(_lecturerUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Lecturer");
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _lecturerUserId,
            Role = ProjectRole.Lecturer
        });

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Submitted,
            SubmittedBy = _leaderUserId,
            SnapshotJson = "[]"
        };
        await _reviewRepository.AddAsync(review);
        await _reviewRepository.SaveChangesAsync();

        var result = await _service.RequestChangesAsync(review.Id, new ReviewDecisionRequest { Note = "Fix diagram 1" });

        Assert.Equal(ReviewStatus.ChangesRequested, result.Status);
        Assert.Equal(_lecturerUserId, result.DecidedBy);
        Assert.Equal("Fix diagram 1", result.Note);
    }

    [Fact]
    [Trait("BR", "BR-30")]
    public async Task ApproveReview_AlreadyDecided_ThrowsBadRequest()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_lecturerUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Lecturer");
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _lecturerUserId,
            Role = ProjectRole.Lecturer
        });

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Approved,
            SubmittedBy = _leaderUserId,
            DecidedBy = _lecturerUserId,
            SnapshotJson = "[]"
        };
        await _reviewRepository.AddAsync(review);
        await _reviewRepository.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ApproveReviewAsync(review.Id));
        Assert.Equal("INVALID_STATE", ex.Code);
        Assert.Equal(400, ex.Status);
    }
}
