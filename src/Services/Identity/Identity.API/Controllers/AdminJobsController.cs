using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Service.Jobs;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/admin/identity/jobs")]
[Authorize(Roles = "Admin")]
public class AdminJobsController : ControllerBase
{
    private readonly IEnumerable<IScheduledJob> _jobs;
    private readonly ILogger<AdminJobsController> _logger;

    public AdminJobsController(IEnumerable<IScheduledJob> jobs, ILogger<AdminJobsController> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    /// <summary>
    /// Manually trigger the purge-tokens background cleanup job.
    /// </summary>
    [HttpPost("purge-tokens/run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunPurgeTokens(CancellationToken ct)
    {
        return await ExecuteJobByNameAsync("purge-tokens", ct);
    }

    /// <summary>
    /// Manually trigger a scheduled job by its name.
    /// </summary>
    [HttpPost("{jobName}/run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunJob([FromRoute] string jobName, CancellationToken ct)
    {
        return await ExecuteJobByNameAsync(jobName, ct);
    }

    private async Task<IActionResult> ExecuteJobByNameAsync(string jobName, CancellationToken ct)
    {
        var job = _jobs.FirstOrDefault(j => string.Equals(j.JobName, jobName, StringComparison.OrdinalIgnoreCase));
        if (job == null)
        {
            _logger.LogWarning("Admin requested run of unknown job: {JobName}", jobName);
            return NotFound(new { message = $"Không tìm thấy job có tên '{jobName}'." });
        }

        _logger.LogInformation("Admin manually triggered job: {JobName}", jobName);
        var summary = await job.ExecuteDirectlyAsync(ct);

        return Ok(new
        {
            job = job.JobName,
            message = summary,
            status = "succeeded",
            executedAt = DateTime.UtcNow
        });
    }
}
