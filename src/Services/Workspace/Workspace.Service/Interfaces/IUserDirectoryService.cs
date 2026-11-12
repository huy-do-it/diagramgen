using UMLForge.BuildingBlocks.Web;
using Workspace.Service.DTOs;

namespace Workspace.Service.Interfaces;

public interface IUserDirectoryService
{
    Task<PagedResult<UserDirectoryResponse>> GetUsersAsync(PageQuery query, string? role, CancellationToken ct = default);
    Task<ProjectDetailResponse> AssignLecturerAsync(Guid projectId, AssignLecturerRequest request, CancellationToken ct = default);
}
