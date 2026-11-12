using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class DiagramRepository : GenericRepository<Entities.Diagram>, IDiagramRepository
{
    public DiagramRepository(DiagramDbContext context) : base(context)
    {
    }

    public async Task<Entities.Diagram?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsInProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(d => d.ProjectId == projectId, cancellationToken);
    }
}
