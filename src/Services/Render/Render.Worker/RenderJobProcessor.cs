using UMLForge.Render.Service.Interfaces;

namespace UMLForge.Render.Worker;

public sealed class RenderJobProcessor(IServiceScopeFactory scopeFactory, ILogger<RenderJobProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var worked = await scope.ServiceProvider.GetRequiredService<IRenderJobRunner>().RunNextAsync(stoppingToken);
                if (!worked) await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Mỗi vòng dùng scope mới; lỗi không làm host chết hoặc giả job đã xong.
                logger.LogError("Render processor failed with {ErrorType}; durable jobs remain in SQL. Retrying queue in 2 seconds.", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
