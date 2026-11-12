using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace UMLForge.BuildingBlocks.Web;

/// <summary>Đổi exception thành ProblemDetails (RFC 7807) có thêm <c>code</c> và <c>errors</c>.</summary>
internal sealed class ProblemDetailsExceptionMiddleware
{
    private const int ClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(
        RequestDelegate next,
        IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var problem = exception switch
        {
            BusinessRuleException rule => Problem(rule.Status, rule.Code, rule.Message, rule.Errors),
            UnauthorizedAccessException => Problem(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Bạn cần đăng nhập."),
            BadHttpRequestException bad => Problem(bad.StatusCode, "BAD_REQUEST", bad.Message),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested
                => Problem(ClientClosedRequest, "CLIENT_CLOSED", "Client đã hủy request."),
            _ => Problem(StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Đã có lỗi hệ thống, vui lòng thử lại sau."),
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception khi xử lý {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogInformation("Request bị từ chối {Status} {Code}: {Detail}", problem.Status, problem.Extensions["code"], problem.Detail);

        context.Response.StatusCode = problem.Status!.Value;
        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(
        int status, string code, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };
        problem.Extensions["code"] = code;
        if (errors is not null)
            problem.Extensions["errors"] = errors;
        return problem;
    }
}
