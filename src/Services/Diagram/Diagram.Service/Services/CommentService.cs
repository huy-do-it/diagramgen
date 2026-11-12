using System.Text.Json;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Events;
using UMLForge.Diagram.Service.Exceptions;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Service.Services;

public class CommentService : ICommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IOutboxRepository? _outboxRepository;

    public CommentService(
        ICommentRepository commentRepository,
        IDiagramRepository diagramRepository,
        IMemberAccessRepository memberAccessRepository,
        ICurrentUser currentUser,
        IOutboxRepository? outboxRepository = null)
    {
        _commentRepository = commentRepository;
        _diagramRepository = diagramRepository;
        _memberAccessRepository = memberAccessRepository;
        _currentUser = currentUser;
        _outboxRepository = outboxRepository;
    }

    public async Task<CommentResponse> AddCommentAsync(
        Guid diagramId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        // BR-32: Mọi thành viên trong project (Leader, Member, Lecturer) đều được comment
        await EnsureProjectAccessAsync(diagram.ProjectId, cancellationToken);

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessRuleException("VALIDATION_ERROR", "Comment content cannot be empty.", 400);
        }

        if (content.Length > 2000)
        {
            throw new BusinessRuleException("VALIDATION_ERROR", "Comment content cannot exceed 2000 characters.", 400);
        }

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            DiagramId = diagramId,
            AuthorId = _currentUser.UserId,
            Content = content,
            CreatedAt = now
        };

        // Outbox event: CommentAdded
        if (_outboxRepository != null)
        {
            var commentAddedEvent = new CommentAdded(
                comment.Id,
                diagram.Id,
                diagram.ProjectId,
                _currentUser.UserId,
                comment.Content
            );

            var envelope = new EventEnvelope<CommentAdded>(
                Guid.NewGuid(),
                "CommentAdded",
                1,
                now,
                Guid.NewGuid(),
                "diagram-svc",
                commentAddedEvent
            );

            _outboxRepository.AddMessage(new OutboxMessage
            {
                Id = envelope.EventId,
                Stream = StreamNames.Diagram,
                Type = "CommentAdded",
                Envelope = JsonSerializer.Serialize(envelope),
                CreatedAt = now
            });
        }

        await _commentRepository.AddAsync(comment, cancellationToken);
        await _commentRepository.SaveChangesAsync(cancellationToken);

        return new CommentResponse
        {
            Id = comment.Id,
            DiagramId = comment.DiagramId,
            AuthorId = comment.AuthorId,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(
        Guid diagramId,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, cancellationToken);

        var comments = await _commentRepository.GetCommentsByDiagramIdAsync(diagramId, cancellationToken);

        return comments.Select(c => new CommentResponse
        {
            Id = c.Id,
            DiagramId = c.DiagramId,
            AuthorId = c.AuthorId,
            Content = c.Content,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    private async Task EnsureProjectAccessAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin) return;

        var hasAccess = await _memberAccessRepository.HasAccessAsync(projectId, _currentUser.UserId, cancellationToken);
        if (!hasAccess)
        {
            throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
        }
    }
}
