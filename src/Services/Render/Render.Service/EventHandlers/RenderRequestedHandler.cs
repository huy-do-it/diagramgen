using FluentValidation;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Repositories.Repositories;

namespace UMLForge.Render.Service.EventHandlers;

public sealed class RenderRequestedHandler(IRenderJobRepository repository, IValidator<RenderRequested> validator) : IEventHandler<RenderRequested>
{
    public async Task HandleAsync(EventEnvelope<RenderRequested> e, CancellationToken ct)
    {
        var request = e.Payload;
        await validator.ValidateAndThrowAsync(request, ct);

        if (await repository.GetByDiagramVersionAsync(request.DiagramId, request.Version, ct) is not null)
            return;

        // Consumer của Bảo commit job + Inbox rồi ACK. Không SaveChanges hoặc chạy pipeline trong handler.
        repository.Add(new RenderJob
        {
            DiagramId = request.DiagramId, ProjectId = request.ProjectId, DiagramVersion = request.Version,
            DiagramType = request.DiagramType, Title = request.Title, ModelJson = request.ModelJson,
            RequestedBy = request.ActorUserId, CorrelationId = e.CorrelationId == Guid.Empty ? e.EventId : e.CorrelationId
        });
    }
}
