using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UMLForge.BuildingBlocks.Web;
using Workspace.Service.DTOs;
using Workspace.Service.Interfaces;

namespace Workspace.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>
    /// Tạo dự án mới. Người tạo tự động trở thành Leader (BR-06).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var result = await _projectService.CreateProjectAsync(request, ct);
        SetETag(result.RowVersion);
        return CreatedAtAction(nameof(GetProjectById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Lấy danh sách dự án mà người dùng tham gia hoặc làm giảng viên hướng dẫn.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProjects([FromQuery] PageQuery query, [FromQuery] string? role, CancellationToken ct)
    {
        var result = await _projectService.GetProjectsAsync(query, role, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết dự án theo ID. Kèm ETag hỗ trợ kiểm tra phiên bản.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetProjectByIdAsync(id, ct);
        SetETag(result.RowVersion);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật thông tin dự án. Chỉ Leader mới có quyền (BR-08), yêu cầu header If-Match.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProject(
        [FromRoute] Guid id,
        [FromBody] UpdateProjectRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var result = await _projectService.UpdateProjectAsync(id, request, ifMatch, ct);
        SetETag(result.RowVersion);
        return Ok(result);
    }

    /// <summary>
    /// Xóa mềm dự án. Chỉ Leader mới có quyền (BR-08).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject([FromRoute] Guid id, CancellationToken ct)
    {
        await _projectService.DeleteProjectAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Lấy danh sách thành viên trong dự án.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetMembersAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Xóa thành viên khỏi dự án. Chỉ Leader mới có quyền, không được xóa Leader (BR-08).
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember([FromRoute] Guid id, [FromRoute] Guid userId, CancellationToken ct)
    {
        await _projectService.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }

    private void SetETag(byte[]? rowVersion)
    {
        if (rowVersion != null && rowVersion.Length > 0)
        {
            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(rowVersion)}\"";
        }
    }
}
