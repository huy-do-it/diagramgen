using UMLForge.Render.Repositories.Entities;

namespace UMLForge.Render.Repositories.Repositories;

public interface IRenderJobRepository
{
    Task<RenderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RenderJob?> GetByDiagramVersionAsync(Guid diagramId, int version, CancellationToken cancellationToken = default);
    void Add(RenderJob job);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
