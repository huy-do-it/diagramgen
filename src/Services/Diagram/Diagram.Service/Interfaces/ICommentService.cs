using UMLForge.Diagram.Service.DTOs;

namespace UMLForge.Diagram.Service.Interfaces;

public interface ICommentService
{
    Task<CommentResponse> AddCommentAsync(Guid diagramId, CreateCommentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(Guid diagramId, CancellationToken cancellationToken = default);
}
