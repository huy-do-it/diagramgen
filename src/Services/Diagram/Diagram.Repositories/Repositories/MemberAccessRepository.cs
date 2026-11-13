using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class MemberAccessRepository : GenericRepository<MemberAccess>, IMemberAccessRepository
{
    public MemberAccessRepository(DiagramDbContext context) : base(context)
    {
    }

    public async Task<MemberAccess?> GetAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    public async Task<bool> HasAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    public async Task<bool> IsLecturerAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.Role == ProjectRole.Lecturer, cancellationToken);
    }
}
