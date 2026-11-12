using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UMLForge.BuildingBlocks.Jobs;

namespace Workspace.API.Controllers;

[ApiController]
[Route("api/admin/workspace/jobs")]
[Authorize(Roles = "Admin")]
public class AdminJobsController : ControllerBase
{
    private readonly IJobTrigger _jobTrigger;

    public AdminJobsController(IJobTrigger jobTrigger)
    {
        _jobTrigger = jobTrigger;
    }

    /// <summary>
    /// Chạy ngay job expire-invitations để quét và đánh dấu lời mời hết hạn (chỉ Admin). Trả về 200 OK.
    /// </summary>
    [HttpPost("expire-invitations/run")]
    [ProducesResponseType(typeof(JobRunResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunExpireInvitations(CancellationToken ct)
    {
        var result = await _jobTrigger.RunNowAsync("expire-invitations", ct);
        if (result == null)
            return NotFound(new { message = "Không tìm thấy job 'expire-invitations'." });

        return Ok(result);
    }
}
