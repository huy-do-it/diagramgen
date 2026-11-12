using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Workspace.Service.DTOs;
using Workspace.Service.Interfaces;

namespace Workspace.API.Controllers;

[ApiController]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>
    /// Gửi lời mời tham gia dự án. Chỉ Leader mới có quyền (BR-08), kiểm tra sĩ số dưới 5 (BR-07).
    /// </summary>
    [HttpPost("api/projects/{id:guid}/invitations")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteMember([FromRoute] Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        var result = await _invitationService.InviteMemberAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lấy danh sách lời mời đang chờ gửi đến người dùng hiện tại.
    /// </summary>
    [HttpGet("api/invitations/mine")]
    [ProducesResponseType(typeof(IReadOnlyList<MyInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyInvitations(CancellationToken ct)
    {
        var result = await _invitationService.GetMyInvitationsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Chấp nhận lời mời tham gia dự án. Kiểm tra hạn 7 ngày (BR-09) và sĩ số tối đa 5 (BR-07).
    /// </summary>
    [HttpPost("api/invitations/{id:guid}/accept")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> AcceptInvitation([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _invitationService.AcceptInvitationAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Từ chối lời mời tham gia dự án.
    /// </summary>
    [HttpPost("api/invitations/{id:guid}/decline")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclineInvitation([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _invitationService.DeclineInvitationAsync(id, ct);
        return Ok(result);
    }
}
