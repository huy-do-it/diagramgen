using UMLForge.Diagram.Service.Events;

namespace UMLForge.Diagram.Service.EventHandlers;

public interface IRenderEventsHandler
{
    Task<bool> HandleRenderStartedAsync(EventEnvelope<RenderStarted> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleRenderCompletedAsync(EventEnvelope<RenderCompleted> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleRenderFailedAsync(EventEnvelope<RenderFailed> envelope, CancellationToken cancellationToken = default);
}
