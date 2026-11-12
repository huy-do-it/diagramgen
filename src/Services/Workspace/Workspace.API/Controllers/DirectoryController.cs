using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UMLForge.BuildingBlocks.Web;
using Workspace.Service.DTOs;
using Workspace.Service.Interfaces;

namespace Workspace.API.Controllers;

[ApiController]
[Authorize]
public class DirectoryController : ControllerBase
{
    private readonly IUserDirectoryService _userDirectoryService;

    public DirectoryController(IUserDirectoryService userDirectoryService)
    {
        _userDirectoryService = userDirectoryService;
    }

    /// <summary>
    /// Tìm kiếm và phân trang người dùng trong danh bạ người dùng replicated.
    /// </summary>
    [HttpGet("api/directory/users")]
    [ProducesResponseType(typeof(PagedResult<UserDirectoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsers([FromQuery] PageQuery query, [FromQuery] string? role, CancellationToken ct)
    {
        var result = await _userDirectoryService.GetUsersAsync(query, role, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gán giảng viên hướng dẫn cho dự án. Yêu cầu vai trò Lecturer trong danh bạ (BR-10).
    /// </summary>
    [HttpPut("api/projects/{id:guid}/lecturer")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignLecturer(
        [FromRoute] Guid id,
        [FromBody] AssignLecturerRequest request,
        CancellationToken ct)
    {
        var result = await _userDirectoryService.AssignLecturerAsync(id, request, ct);
        return Ok(result);
    }
}
