using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IDiagramVersionRepository : IGenericRepository<DiagramVersion>
{
    Task<DiagramVersion?> GetByVersionAsync(Guid diagramId, int version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiagramVersion>> GetVersionsPagedAsync(Guid diagramId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountVersionsAsync(Guid diagramId, CancellationToken cancellationToken = default);
}
