using UMLForge.BuildingBlocks.Web;
using Workspace.Service.DTOs;

namespace Workspace.Service.Interfaces;

public interface IProjectService
{
    Task<ProjectDetailResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<PagedResult<ProjectSummaryResponse>> GetProjectsAsync(PageQuery query, string? role, CancellationToken ct = default);
    Task<ProjectDetailResponse> GetProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDetailResponse> UpdateProjectAsync(Guid id, UpdateProjectRequest request, string? ifMatch, CancellationToken ct = default);
    Task DeleteProjectAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMemberResponse>> GetMembersAsync(Guid projectId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid projectId, Guid memberUserId, CancellationToken ct = default);
}
