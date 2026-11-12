using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface ICommentRepository : IGenericRepository<Comment>
{
    Task<IReadOnlyList<Comment>> GetCommentsByDiagramIdAsync(Guid diagramId, CancellationToken cancellationToken = default);
}
