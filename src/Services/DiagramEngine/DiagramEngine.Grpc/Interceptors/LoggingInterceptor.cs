using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog.Context;
using UMLForge.Contracts.Events;

namespace UMLForge.DiagramEngine.Grpc.Interceptors;

/// <summary>
/// Log tên RPC + mã kết quả + thời gian; đọc <c>x-correlation-id</c> từ metadata (tạo mới nếu thiếu),
/// gắn vào log và trả lại trong trailer để phía gọi lần theo được.
/// </summary>
public sealed class LoggingInterceptor : Interceptor
{
    private const string HealthMethodPrefix = "/grpc.health.";

    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        => InvokeAsync(context, () => continuation(request, context));

    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
        => InvokeAsync(context, async () =>
        {
            await continuation(request, responseStream, context);
            return true;
        });

    private async Task<T> InvokeAsync<T>(ServerCallContext context, Func<Task<T>> call)
    {
        var correlationId = Guid.TryParse(context.RequestHeaders.GetValue(CorrelationKeys.GrpcMetadata), out var id)
            ? id
            : Guid.NewGuid();
        context.ResponseTrailers.Add(CorrelationKeys.GrpcMetadata, correlationId.ToString());

        using var activity = CorrelationContext.Begin(correlationId, "umlforge.grpc");
        using var logProperty = LogContext.PushProperty(CorrelationKeys.LogProperty, correlationId);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await call();
            // Healthcheck Docker gọi 10 s/lần → Debug để không lấn log nghiệp vụ
            var level = context.Method.StartsWith(HealthMethodPrefix, StringComparison.Ordinal) ? LogLevel.Debug : LogLevel.Information;
            _logger.Log(level, "gRPC {Method} OK in {ElapsedMs} ms", context.Method, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("gRPC {Method} {StatusCode} in {ElapsedMs} ms: {Detail}",
                context.Method, ex.StatusCode, stopwatch.ElapsedMilliseconds, ex.Status.Detail);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "gRPC {Method} lỗi sau {ElapsedMs} ms", context.Method, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
