using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.Events;

namespace UMLForge.Diagram.Service.EventHandlers;

public class WorkspaceEventsHandler : IWorkspaceEventsHandler
{
    private const string ConsumerName = "diagram-svc";

    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IInboxRepository _inboxRepository;

    public WorkspaceEventsHandler(
        IMemberAccessRepository memberAccessRepository,
        IDiagramRepository diagramRepository,
        IInboxRepository inboxRepository)
    {
        _memberAccessRepository = memberAccessRepository;
        _diagramRepository = diagramRepository;
        _inboxRepository = inboxRepository;
    }

    public async Task<bool> HandleProjectCreatedAsync(EventEnvelope<ProjectCreated> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        // Chủ project ngầm định là Leader
        var access = await _memberAccessRepository.GetAccessAsync(payload.ProjectId, payload.OwnerId, cancellationToken);
        if (access == null)
        {
            await _memberAccessRepository.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.OwnerId,
                Role = ProjectRole.Leader
            }, cancellationToken);
        }
        else
        {
            access.Role = ProjectRole.Leader;
            _memberAccessRepository.Update(access);
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleMemberAddedAsync(EventEnvelope<MemberAdded> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        var access = await _memberAccessRepository.GetAccessAsync(payload.ProjectId, payload.UserId, cancellationToken);
        if (access == null)
        {
            await _memberAccessRepository.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.UserId,
                Role = payload.Role
            }, cancellationToken);
        }
        else
        {
            access.Role = payload.Role;
            _memberAccessRepository.Update(access);
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleMemberRemovedAsync(EventEnvelope<MemberRemoved> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;
        var access = await _memberAccessRepository.GetAccessAsync(payload.ProjectId, payload.UserId, cancellationToken);
        if (access != null)
        {
            _memberAccessRepository.Delete(access);
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleLecturerAssignedAsync(EventEnvelope<LecturerAssigned> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;

        // Xóa quyền của Lecturer cũ nếu có
        if (payload.PreviousLecturerId.HasValue && payload.PreviousLecturerId.Value != Guid.Empty)
        {
            var oldAccess = await _memberAccessRepository.GetAccessAsync(payload.ProjectId, payload.PreviousLecturerId.Value, cancellationToken);
            if (oldAccess != null)
            {
                _memberAccessRepository.Delete(oldAccess);
            }
        }

        // Gán quyền Lecturer mới
        var newAccess = await _memberAccessRepository.GetAccessAsync(payload.ProjectId, payload.LecturerId, cancellationToken);
        if (newAccess == null)
        {
            await _memberAccessRepository.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.LecturerId,
                Role = ProjectRole.Lecturer
            }, cancellationToken);
        }
        else
        {
            newAccess.Role = ProjectRole.Lecturer;
            _memberAccessRepository.Update(newAccess);
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HandleProjectDeletedAsync(EventEnvelope<ProjectDeleted> envelope, CancellationToken cancellationToken = default)
    {
        if (await _inboxRepository.HasBeenProcessedAsync(envelope.EventId, ConsumerName, cancellationToken))
        {
            return false;
        }

        var payload = envelope.Payload;

        // Xóa toàn bộ MemberAccess của project
        var accesses = await _memberAccessRepository.GetQueryable()
            .Where(m => m.ProjectId == payload.ProjectId)
            .ToListAsync(cancellationToken);

        foreach (var access in accesses)
        {
            _memberAccessRepository.Delete(access);
        }

        // Xóa mềm toàn bộ diagram trong project
        var diagrams = await _diagramRepository.GetQueryable()
            .Where(d => d.ProjectId == payload.ProjectId)
            .ToListAsync(cancellationToken);

        foreach (var diagram in diagrams)
        {
            diagram.IsDeleted = true;
            diagram.UpdatedAt = DateTime.UtcNow;
            _diagramRepository.Update(diagram);
        }

        await _inboxRepository.MarkProcessedAsync(envelope.EventId, ConsumerName, cancellationToken);
        await _memberAccessRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
