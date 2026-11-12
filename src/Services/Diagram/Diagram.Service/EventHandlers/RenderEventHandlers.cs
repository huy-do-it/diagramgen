using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Service.EventHandlers;

public sealed class RenderStartedHandler(IDiagramRepository diagramRepo) : IEventHandler<RenderStarted>
{
    public async Task HandleAsync(EventEnvelope<RenderStarted> e, CancellationToken ct)
    {
        var payload = e.Payload;
        var diagram = await diagramRepo.GetByIdAsync(payload.DiagramId, ct);
        if (diagram == null) return;
        // Bỏ qua sự kiện có version < CurrentVersion (diagram đã được sửa sau khi gửi render)
        if (payload.Version < diagram.CurrentVersion) return;

        diagram.Status = DiagramStatus.Rendering;
        diagram.UpdatedAt = DateTime.UtcNow;
        diagramRepo.Update(diagram);
    }
}

public sealed class RenderCompletedHandler(IDiagramRepository diagramRepo) : IEventHandler<RenderCompleted>
{
    public async Task HandleAsync(EventEnvelope<RenderCompleted> e, CancellationToken ct)
    {
        var payload = e.Payload;
        var diagram = await diagramRepo.GetByIdAsync(payload.DiagramId, ct);
        if (diagram == null) return;
        if (payload.Version < diagram.CurrentVersion) return;

        diagram.Status = DiagramStatus.Rendered;
        diagram.SvgXml = payload.Svg;
        diagram.PlantUml = payload.PlantUml;
        diagram.RenderedAt = DateTime.UtcNow;
        diagram.UpdatedAt = DateTime.UtcNow;
        diagramRepo.Update(diagram);
    }
}

public sealed class RenderFailedHandler(IDiagramRepository diagramRepo) : IEventHandler<RenderFailed>
{
    public async Task HandleAsync(EventEnvelope<RenderFailed> e, CancellationToken ct)
    {
        var payload = e.Payload;
        var diagram = await diagramRepo.GetByIdAsync(payload.DiagramId, ct);
        if (diagram == null) return;
        if (payload.Version < diagram.CurrentVersion) return;

        diagram.Status = DiagramStatus.Failed;
        diagram.LastError = payload.Error;
        diagram.UpdatedAt = DateTime.UtcNow;
        // Giữ nguyên SvgXml cũ theo BR-28
        diagramRepo.Update(diagram);
    }
}
