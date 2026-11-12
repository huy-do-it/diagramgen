using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMLForge.Notification.Service.Interfaces;
using UMLForge.Notification.Service.Models;

namespace UMLForge.Notification.API.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    /// <summary>Danh sách thông báo của người dùng hiện tại, có lọc và phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationPage>> GetPage([FromQuery] NotificationQuery query, CancellationToken cancellationToken)
        => Ok(await service.GetPageAsync(UserId, query, cancellationToken));

    /// <summary>Đếm thông báo chưa đọc của người dùng hiện tại.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
        => Ok(new UnreadCountResponse(await service.CountUnreadAsync(UserId, cancellationToken)));

    /// <summary>Đánh dấu một thông báo là đã đọc; thông báo người khác trả 403 BR-33.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(MarkReadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkReadResponse>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await service.MarkReadAsync(UserId, id, cancellationToken);
        if (outcome == MarkReadOutcome.Forbidden)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, 403, title: "Bạn không có quyền đọc thông báo này.");
            problem.Extensions["code"] = "BR-33";
            return new ObjectResult(problem) { StatusCode = 403 };
        }
        return outcome switch
        {
            MarkReadOutcome.NotFound => Problem(statusCode: 404, title: "Không tìm thấy thông báo."),
            _ => Ok(new MarkReadResponse(true))
        };
    }

    /// <summary>Đánh dấu mọi thông báo chưa đọc của người dùng hiện tại.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(MarkAllReadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkAllReadResponse>> MarkAllRead(CancellationToken cancellationToken)
        => Ok(new MarkAllReadResponse(await service.MarkAllReadAsync(UserId, cancellationToken)));
}
