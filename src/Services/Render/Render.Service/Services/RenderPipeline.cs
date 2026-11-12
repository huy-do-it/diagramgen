using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Service.Interfaces;
using UMLForge.Render.Service.Models;
using UMLForge.Contracts.Events;

namespace UMLForge.Render.Service.Services;

public sealed class RenderPipeline(
    IDiagramEngineClient engine,
    PlantUmlRenderer renderer,
    IOptions<RenderOptions> options,
    ILogger<RenderPipeline> logger) : IRenderPipeline
{
    public async Task<RenderResult> ProcessJobAsync(RenderJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        var started = Stopwatch.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var token = deadline.Token;

        // Snapshot đầu vào; không mutate entity hoặc tự commit trước Outbox của M-03.
        var type = job.DiagramType;
        var modelJson = job.ModelJson;
        var title = job.Title;
        try
        {
            if (type is < 1 or > 5 || string.IsNullOrWhiteSpace(modelJson)
                || Encoding.UTF8.GetByteCount(modelJson) > 256 * 1024)
                return Failure("Mô hình sơ đồ không hợp lệ.");
            using var model = JsonDocument.Parse(modelJson);
            if (model.RootElement.ValueKind != JsonValueKind.Object)
                return Failure("Mô hình sơ đồ phải là JSON object.");

            if (!await engine.ValidateAsync(type, modelJson, token))
                return Failure("Mô hình sơ đồ không đạt kiểm tra UML.");
            token.ThrowIfCancellationRequested();
            var plantUml = await engine.GeneratePlantUmlAsync(type, modelJson, title, token);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(plantUml))
                return Failure("Dịch vụ UML không trả về mã PlantUML hợp lệ.");
            var svg = await renderer.RenderSvgAsync(plantUml, token);
            token.ThrowIfCancellationRequested();
            return new RenderResult(plantUml, svg, null, ElapsedMs());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown không phải lỗi UML; M-03/M-04 quyết định phục hồi job.
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return Failure("Render vượt quá thời gian cho phép.");
        }
        catch (RpcException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded
            || ex.StatusCode == StatusCode.Cancelled && deadline.IsCancellationRequested)
        {
            return Failure("Dịch vụ UML vượt quá thời gian cho phép.");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            return Failure("Mô hình sơ đồ không đạt kiểm tra UML.");
        }
        catch (JsonException)
        {
            return Failure("Mô hình sơ đồ không phải JSON hợp lệ.");
        }
        catch (InvalidDataException)
        {
            return Failure("Dịch vụ render trả về SVG không hợp lệ.");
        }
        catch (TimeoutException)
        {
            return Failure("Render vượt quá thời gian cho phép.");
        }
        catch (HttpRequestException)
        {
            return Failure("Dịch vụ render tạm thời không phản hồi hoặc trả về lỗi.");
        }

        long ElapsedMs() => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        RenderResult Failure(string error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("Render Job {JobId}, CorrelationId {CorrelationId} failed: {Reason}",
                job.Id, CorrelationContext.Current?.ToString() ?? Activity.Current?.TraceId.ToString(), error);
            return new RenderResult(null, null, error, ElapsedMs());
        }
    }
}
