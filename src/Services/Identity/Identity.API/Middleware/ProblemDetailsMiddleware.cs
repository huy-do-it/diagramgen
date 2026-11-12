using System.Diagnostics;
using System.Text.Json;
using Identity.Service.Exceptions;

namespace Identity.API.Middleware;

public class ProblemDetailsDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public IDictionary<string, string[]>? Errors { get; set; }
}

public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            await HandleBusinessRuleExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Path}", context.Request.Path);
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private static async Task HandleBusinessRuleExceptionAsync(HttpContext context, BusinessRuleException ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = new ProblemDetailsDto
        {
            Type = $"https://umlforge.dev/errors/{ex.Code.ToLowerInvariant()}",
            Title = GetTitleForStatusCode(ex.StatusCode),
            Status = ex.StatusCode,
            Detail = ex.Message,
            Code = ex.Code,
            TraceId = traceId,
            Errors = ex.Errors
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = ex.StatusCode;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }

    private static async Task HandleGenericExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = new ProblemDetailsDto
        {
            Type = "https://umlforge.dev/errors/internal-server-error",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng thử lại sau.",
            Code = "INTERNAL_ERROR",
            TraceId = traceId
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status410Gone => "Gone",
        _ => "Error"
    };
}
