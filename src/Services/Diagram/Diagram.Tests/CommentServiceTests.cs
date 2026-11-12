using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Exceptions;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;
using Xunit;
using DiagramEntity = UMLForge.Diagram.Repositories.Entities.Diagram;

namespace UMLForge.Diagram.Tests;

public class CommentServiceTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly ICommentRepository _commentRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IOutboxRepository> _outboxMock;
    private readonly CommentService _service;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _testDiagramId = Guid.NewGuid();
    private readonly Guid _studentUserId = Guid.NewGuid();
    private readonly Guid _lecturerUserId = Guid.NewGuid();
    private readonly Guid _outsiderUserId = Guid.NewGuid();

    public CommentServiceTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _commentRepository = new CommentRepository(_dbContext);
        _diagramRepository = new DiagramRepository(_dbContext);
        _memberAccessRepository = new MemberAccessRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUser>();
        _outboxMock = new Mock<IOutboxRepository>();

        _currentUserMock.Setup(u => u.UserId).Returns(_studentUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new CommentService(
            _commentRepository,
            _diagramRepository,
            _memberAccessRepository,
            _currentUserMock.Object,
            _outboxMock.Object);

        // Seed a diagram
        _dbContext.Diagrams.Add(new DiagramEntity
        {
            Id = _testDiagramId,
            ProjectId = _testProjectId,
            Title = "Test Diagram",
            Type = DiagramType.UseCase,
            Status = DiagramStatus.Rendered,
            ModelJson = "{}"
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task AddComment_ValidStudent_CreatesComment()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _studentUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateCommentRequest { Content = "Great diagram!" };
        var result = await _service.AddCommentAsync(_testDiagramId, request);

        Assert.NotNull(result);
        Assert.Equal(_testDiagramId, result.DiagramId);
        Assert.Equal(_studentUserId, result.AuthorId);
        Assert.Equal("Great diagram!", result.Content);
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task AddComment_LecturerUser_CanAddComment()
    {
        // Setup lecturer access
        _currentUserMock.Setup(u => u.UserId).Returns(_lecturerUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Lecturer");

        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _lecturerUserId,
            Role = ProjectRole.Lecturer
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateCommentRequest { Content = "Please review the associations." };
        var result = await _service.AddCommentAsync(_testDiagramId, request);

        Assert.NotNull(result);
        Assert.Equal(_testDiagramId, result.DiagramId);
        Assert.Equal(_lecturerUserId, result.AuthorId);
        Assert.Equal("Please review the associations.", result.Content);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task AddComment_UserWithoutAccess_ThrowsForbidden()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_outsiderUserId);

        var request = new CreateCommentRequest { Content = "Unauthorized comment." };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AddCommentAsync(_testDiagramId, request));
        Assert.Equal("BR-11", ex.Code);
        Assert.Equal(403, ex.Status);
    }

    [Fact]
    public async Task AddComment_EmptyContent_ThrowsBadRequest()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _studentUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateCommentRequest { Content = "   " };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AddCommentAsync(_testDiagramId, request));
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task GetComments_ValidDiagram_ReturnsCommentsList()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _studentUserId,
            Role = ProjectRole.Member
        });
        await _commentRepository.AddAsync(new Comment
        {
            Id = Guid.NewGuid(),
            DiagramId = _testDiagramId,
            AuthorId = _studentUserId,
            Content = "First comment"
        });
        await _commentRepository.AddAsync(new Comment
        {
            Id = Guid.NewGuid(),
            DiagramId = _testDiagramId,
            AuthorId = _studentUserId,
            Content = "Second comment"
        });
        await _commentRepository.SaveChangesAsync();

        var comments = await _service.GetCommentsAsync(_testDiagramId);

        Assert.Equal(2, comments.Count);
    }
}
