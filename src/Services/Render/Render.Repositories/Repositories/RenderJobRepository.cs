using Microsoft.EntityFrameworkCore;
using UMLForge.Render.Repositories.Entities;

namespace UMLForge.Render.Repositories.Repositories;

public sealed class RenderJobRepository(RenderDbContext context) : IRenderJobRepository
{
    public Task<RenderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.RenderJobs.Include(x => x.Artifact).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<RenderJob?> GetByDiagramVersionAsync(Guid diagramId, int version, CancellationToken cancellationToken = default)
        => context.RenderJobs.Include(x => x.Artifact)
            .SingleOrDefaultAsync(x => x.DiagramId == diagramId && x.DiagramVersion == version, cancellationToken);

    public void Add(RenderJob job) => context.RenderJobs.Add(job);

    public Task<RenderJob?> GetNextQueuedAsync(CancellationToken cancellationToken = default)
        => context.RenderJobs.Include(x => x.Artifact).Where(x => x.Status == RenderJobStatus.Queued)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
