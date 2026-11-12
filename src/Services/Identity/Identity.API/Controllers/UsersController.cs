using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Service.DTOs;
using Identity.Service.Exceptions;
using Identity.Service.Interfaces;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public UsersController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var user = await _authService.GetCurrentUserAsync(userId, ct);
        return Ok(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isLocked,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _userService.GetPagedUsersAsync(search, role, isLocked, sort, page, pageSize, ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/lock")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LockUser([FromRoute] Guid id, [FromBody] LockUserRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var user = await _userService.LockUserAsync(adminId, id, request.IsLocked, ct);
        return Ok(user);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole([FromRoute] Guid id, [FromBody] ChangeRoleRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var user = await _userService.ChangeRoleAsync(adminId, id, request.Role, ct);
        return Ok(user);
    }

    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var userId))
        {
            throw new BusinessRuleException("UNAUTHORIZED", "Không thể xác định danh tính người dùng.", statusCode: 401);
        }

        return userId;
    }
}
