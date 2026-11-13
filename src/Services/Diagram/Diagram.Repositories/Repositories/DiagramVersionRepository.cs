using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class DiagramVersionRepository : GenericRepository<DiagramVersion>, IDiagramVersionRepository
{
    public DiagramVersionRepository(DiagramDbContext context) : base(context)
    {
    }

    public async Task<DiagramVersion?> GetByVersionAsync(Guid diagramId, int version, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.DiagramId == diagramId && v.Version == version, cancellationToken);
    }

    public async Task<IReadOnlyList<DiagramVersion>> GetVersionsPagedAsync(Guid diagramId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(v => v.DiagramId == diagramId)
            .OrderByDescending(v => v.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountVersionsAsync(Guid diagramId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.DiagramId == diagramId)
            .CountAsync(cancellationToken);
    }
}
