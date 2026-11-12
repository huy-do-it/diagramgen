using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public interface IProjectRepository : IGenericRepository<Project>
{
    Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Project> Items, int TotalCount)> GetPagedProjectsForUserAsync(
        Guid userId, string? search, string? role, string? sort, int page, int pageSize, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<bool> IsLeaderAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<ProjectMember?> GetMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, CancellationToken ct = default);
    Task AddMemberAsync(ProjectMember member, CancellationToken ct = default);
    void RemoveMember(ProjectMember member);
    Task<int> GetActiveStudentMemberCountAsync(Guid projectId, CancellationToken ct = default);
    Task<int> GetActiveMemberCountAsync(Guid projectId, CancellationToken ct = default);
}
