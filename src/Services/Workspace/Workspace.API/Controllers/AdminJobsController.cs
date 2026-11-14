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
    /// Chạy ngay job expire-invitations (chỉ Admin), trả JobRunResult với 202 như shared job endpoint.
    /// </summary>
    [HttpPost("expire-invitations/run")]
    [ProducesResponseType(typeof(JobRunResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunExpireInvitations(CancellationToken ct)
    {
        var result = await _jobTrigger.RunNowAsync("expire-invitations", ct);
        if (result == null)
        {
            var response = Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "Không có job 'expire-invitations'.");
            ((ProblemDetails)response.Value!).Extensions["code"] = "JOB_NOT_FOUND";
            return response;
        }

        return Accepted(value: result);
    }
}
