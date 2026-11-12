using UMLForge.Diagram.Service.Events;

namespace UMLForge.Diagram.Service.EventHandlers;

public interface IWorkspaceEventsHandler
{
    Task<bool> HandleProjectCreatedAsync(EventEnvelope<ProjectCreated> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleMemberAddedAsync(EventEnvelope<MemberAdded> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleMemberRemovedAsync(EventEnvelope<MemberRemoved> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleLecturerAssignedAsync(EventEnvelope<LecturerAssigned> envelope, CancellationToken cancellationToken = default);
    Task<bool> HandleProjectDeletedAsync(EventEnvelope<ProjectDeleted> envelope, CancellationToken cancellationToken = default);
}
