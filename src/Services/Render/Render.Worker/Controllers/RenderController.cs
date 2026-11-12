using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMLForge.Render.Service.Models;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Service.Services;
namespace UMLForge.Render.Worker.Controllers;
[ApiController, Authorize(Roles="Admin"), Route("api/render")]
[ProducesResponseType(typeof(ProblemDetails),401), ProducesResponseType(typeof(ProblemDetails),403)]
public sealed class RenderController(RenderOperationsService service) : ControllerBase
{
    /// <summary>Danh sách job, khoảng thời gian UTC [from,to).</summary>
    [HttpGet("jobs"), ProducesResponseType(typeof(PagedResult<RenderJobResponse>),200), ProducesResponseType(typeof(ProblemDetails),400)]
    public async Task<IActionResult> Jobs([FromQuery] RenderQuery query, CancellationToken ct) => Ok(await service.JobsAsync(query,ct));
    /// <summary>Xếp lại job Failed từ snapshot cũ; Attempt tăng khi processor claim.</summary>
    [HttpPost("jobs/{id:guid}/retry"), ProducesResponseType(202), ProducesResponseType(typeof(ProblemDetails),404), ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) { await service.RetryAsync(id,ct); return Accepted(new {jobId=id}); }
    /// <summary>Báo cáo ngày, khoảng thời gian UTC [from,to).</summary>
    [HttpGet("reports"), ProducesResponseType(typeof(PagedResult<RenderDailyReport>),200), ProducesResponseType(typeof(ProblemDetails),400)]
    public async Task<IActionResult> Reports([FromQuery] RenderQuery query, CancellationToken ct) => Ok(await service.ReportsAsync(query,ct));
}
