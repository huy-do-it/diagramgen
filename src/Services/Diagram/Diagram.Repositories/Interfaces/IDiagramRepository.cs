namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IDiagramRepository : IGenericRepository<Entities.Diagram>
{
    Task<Entities.Diagram?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsInProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
