using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IMemberAccessRepository : IGenericRepository<MemberAccess>
{
    Task<MemberAccess?> GetAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsLecturerAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
