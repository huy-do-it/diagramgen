using Microsoft.AspNetCore.Http;
using Serilog.Context;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Web;

/// <summary>Lấy X-Correlation-Id từ request (tạo mới nếu thiếu), trả lại trong response và gắn vào log.</summary>
internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers[CorrelationKeys.HttpHeader], out var id)
            ? id
            : Guid.NewGuid();

        context.Response.Headers[CorrelationKeys.HttpHeader] = correlationId.ToString();

        using var activity = CorrelationContext.Begin(correlationId, "umlforge.http");
        using (LogContext.PushProperty(CorrelationKeys.LogProperty, correlationId))
        {
            await _next(context);
        }
    }
}
