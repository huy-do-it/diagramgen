using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Service.EventHandlers;

public sealed class ProjectCreatedHandler(IMemberAccessRepository memberAccessRepo) : IEventHandler<ProjectCreated>
{
    public async Task HandleAsync(EventEnvelope<ProjectCreated> e, CancellationToken ct)
    {
        var payload = e.Payload;
        // Chủ project ngầm định là Leader (TASKS §1.5)
        var access = await memberAccessRepo.GetAccessAsync(payload.ProjectId, payload.OwnerId, ct);
        if (access == null)
        {
            await memberAccessRepo.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.OwnerId,
                Role = ProjectRole.Leader
            }, ct);
        }
        else
        {
            access.Role = ProjectRole.Leader;
            memberAccessRepo.Update(access);
        }
    }
}

public sealed class MemberAddedHandler(IMemberAccessRepository memberAccessRepo) : IEventHandler<MemberAdded>
{
    public async Task HandleAsync(EventEnvelope<MemberAdded> e, CancellationToken ct)
    {
        var payload = e.Payload;
        var role = string.IsNullOrWhiteSpace(payload.Role) ? ProjectRole.Member : payload.Role;
        var access = await memberAccessRepo.GetAccessAsync(payload.ProjectId, payload.UserId, ct);
        if (access == null)
        {
            await memberAccessRepo.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.UserId,
                Role = role
            }, ct);
        }
        else
        {
            access.Role = role;
            memberAccessRepo.Update(access);
        }
    }
}

public sealed class MemberRemovedHandler(IMemberAccessRepository memberAccessRepo) : IEventHandler<MemberRemoved>
{
    public async Task HandleAsync(EventEnvelope<MemberRemoved> e, CancellationToken ct)
    {
        var access = await memberAccessRepo.GetAccessAsync(e.Payload.ProjectId, e.Payload.UserId, ct);
        if (access != null)
        {
            memberAccessRepo.Delete(access);
        }
    }
}

public sealed class LecturerAssignedHandler(IMemberAccessRepository memberAccessRepo) : IEventHandler<LecturerAssigned>
{
    public async Task HandleAsync(EventEnvelope<LecturerAssigned> e, CancellationToken ct)
    {
        var payload = e.Payload;
        // Xóa giảng viên cũ của đồ án nếu có
        var members = await memberAccessRepo.FindAsync(m => m.ProjectId == payload.ProjectId, ct);
        var oldLecturers = members.Where(m => m.Role == ProjectRole.Lecturer).ToList();
        foreach (var old in oldLecturers)
        {
            memberAccessRepo.Delete(old);
        }

        var access = await memberAccessRepo.GetAccessAsync(payload.ProjectId, payload.LecturerId, ct);
        if (access == null)
        {
            await memberAccessRepo.AddAsync(new MemberAccess
            {
                ProjectId = payload.ProjectId,
                UserId = payload.LecturerId,
                Role = ProjectRole.Lecturer
            }, ct);
        }
        else
        {
            access.Role = ProjectRole.Lecturer;
            memberAccessRepo.Update(access);
        }
    }
}

public sealed class ProjectDeletedHandler(
    IMemberAccessRepository memberAccessRepo,
    IDiagramRepository diagramRepo) : IEventHandler<ProjectDeleted>
{
    public async Task HandleAsync(EventEnvelope<ProjectDeleted> e, CancellationToken ct)
    {
        var projectId = e.Payload.ProjectId;
        var members = await memberAccessRepo.FindAsync(m => m.ProjectId == projectId, ct);
        foreach (var member in members)
        {
            memberAccessRepo.Delete(member);
        }

        var diagrams = await diagramRepo.FindAsync(d => d.ProjectId == projectId, ct);
        foreach (var diagram in diagrams)
        {
            diagram.IsDeleted = true;
            diagram.UpdatedAt = DateTime.UtcNow;
            diagramRepo.Update(diagram);
        }
    }
}
