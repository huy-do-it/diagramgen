using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.Events;

namespace UMLForge.Diagram.Service.EventHandlers;

public class RenderEventsHandler : IRenderEventsHandler
{
    private const string ConsumerName = "diagram-svc";

    private readonly IDiagramRepository _diagramRepository;
    private readonly IInboxRepository _inboxRepository;

    public RenderEventsHandler(
        IDiagramRepository diagramRepository,
        IInboxRepository inboxRepository)
    {
        _diagramRepository = diagramRepository;
        _inboxRepository = inboxRepository;
    }

    public async Task<bool> HandleRenderStartedAsync(EventEnvelope<RenderStarted> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        var diagram = await _diagramRepository.GetByIdAsync(payload.DiagramId, cancellationToken);
        if (diagram != null)
        {
            // Bỏ qua nếu version sự kiện cũ hơn CurrentVersion hiện tại của diagram
            if (payload.Version >= diagram.CurrentVersion)
            {
                diagram.Status = DiagramStatus.Rendering;
                diagram.UpdatedAt = DateTime.UtcNow;
                _diagramRepository.Update(diagram);
            }
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _diagramRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleRenderCompletedAsync(EventEnvelope<RenderCompleted> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        var diagram = await _diagramRepository.GetByIdAsync(payload.DiagramId, cancellationToken);
        if (diagram != null)
        {
            // Bỏ qua nếu version sự kiện cũ hơn CurrentVersion
            if (payload.Version >= diagram.CurrentVersion)
            {
                diagram.SvgXml = payload.Svg;
                diagram.PlantUml = payload.PlantUml;
                diagram.RenderedAt = DateTime.UtcNow;
                diagram.Status = DiagramStatus.Rendered;
                diagram.LastError = null;
                diagram.UpdatedAt = DateTime.UtcNow;
                _diagramRepository.Update(diagram);
            }
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _diagramRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleRenderFailedAsync(EventEnvelope<RenderFailed> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        var diagram = await _diagramRepository.GetByIdAsync(payload.DiagramId, cancellationToken);
        if (diagram != null)
        {
            // Bỏ qua nếu version sự kiện cũ hơn CurrentVersion
            if (payload.Version >= diagram.CurrentVersion)
            {
                diagram.Status = DiagramStatus.Failed;
                diagram.LastError = payload.Error;
                diagram.UpdatedAt = DateTime.UtcNow;
                // BR-28: Render lỗi giữ lại SVG cũ (KHÔNG gán SvgXml = null)
                _diagramRepository.Update(diagram);
            }
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _diagramRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
