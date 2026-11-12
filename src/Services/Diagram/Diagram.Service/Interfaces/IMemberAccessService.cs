using UMLForge.Diagram.Repositories.Entities;

namespace UMLForge.Diagram.Service.Interfaces;

public interface IMemberAccessService
{
    Task GrantAccessAsync(Guid projectId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<MemberAccess?> GetAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
