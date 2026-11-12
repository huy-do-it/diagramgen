using FluentValidation;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Notification.Repositories.Entities;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.Validators;
using NotificationEntity = UMLForge.Notification.Repositories.Entities.Notification;
namespace UMLForge.Notification.Service.EventHandlers;

public sealed class NotificationEventHandler(INotificationEventRepository repository, NotificationEventValidator validator, TimeProvider clock) :
    IEventHandler<ProjectCreated>, IEventHandler<InvitationSent>, IEventHandler<MemberAdded>, IEventHandler<MemberRemoved>, IEventHandler<LecturerAssigned>, IEventHandler<ProjectDeleted>, IEventHandler<ReviewSubmitted>, IEventHandler<ReviewDecided>, IEventHandler<CommentAdded>, IEventHandler<RenderCompleted>, IEventHandler<RenderFailed>
{
    private Task ValidateAsync(object payload, CancellationToken ct) => validator.ValidateAndThrowAsync(payload, ct);
    private void Notify(Guid user, string type, string title, string link)
        => repository.AddNotification(new NotificationEntity { UserId = user, Type = type, Title = title[..Math.Min(title.Length, 200)], Link = link, CreatedAt = clock.GetUtcNow().UtcDateTime });
    private static string ProjectLink(Guid id) => $"/projects/{id}?tab=review";
    private async Task<List<ProjectRecipient>> ReadyRecipientsAsync(Guid project, CancellationToken ct)
    {
        var recipients = await repository.RecipientsAsync(project, ct);
        if (!recipients.Any(x => x.Role == ProjectRoles.Leader)) throw new InvalidOperationException("Project recipients are not synchronized yet.");
        return recipients;
    }
    private void Upsert(List<ProjectRecipient> rows, Guid project, Guid user, string role, string name)
    {
        var row = rows.SingleOrDefault(x => x.UserId == user);
        if (row is null) repository.AddRecipient(new ProjectRecipient { ProjectId = project, UserId = user, Role = role, ProjectName = name });
        else { row.Role = role; row.ProjectName = name; }
        foreach (var existing in rows) existing.ProjectName = name;
    }
    public async Task HandleAsync(EventEnvelope<ProjectCreated> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        Upsert(await repository.RecipientsAsync(x.ProjectId, ct), x.ProjectId, x.OwnerId, ProjectRoles.Leader, x.Name);
    }
    public async Task HandleAsync(EventEnvelope<InvitationSent> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        if (x.InviteeId != x.ActorUserId) Notify(x.InviteeId, e.Type, $"Lời mời tham gia {x.ProjectName}", $"/projects/{x.ProjectId}");
    }
    public async Task HandleAsync(EventEnvelope<MemberAdded> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload; var rows = await ReadyRecipientsAsync(x.ProjectId, ct);
        Upsert(rows, x.ProjectId, x.UserId, x.Role, x.ProjectName);
        foreach (var leader in rows.Where(r => r.Role == ProjectRoles.Leader && r.UserId != x.ActorUserId)) Notify(leader.UserId, e.Type, $"Thành viên mới tham gia {x.ProjectName}", ProjectLink(x.ProjectId));
    }
    public async Task HandleAsync(EventEnvelope<MemberRemoved> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        repository.RemoveRecipients((await repository.RecipientsAsync(x.ProjectId, ct)).Where(r => r.UserId == x.UserId));
    }
    public async Task HandleAsync(EventEnvelope<LecturerAssigned> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload; var rows = await repository.RecipientsAsync(x.ProjectId, ct);
        repository.RemoveRecipients(rows.Where(r => r.Role == ProjectRoles.Lecturer && r.UserId != x.LecturerId));
        Upsert(rows, x.ProjectId, x.LecturerId, ProjectRoles.Lecturer, x.ProjectName);
    }
    public async Task HandleAsync(EventEnvelope<ProjectDeleted> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct); repository.RemoveRecipients(await repository.RecipientsAsync(e.Payload.ProjectId, ct));
    }
    public async Task HandleAsync(EventEnvelope<ReviewSubmitted> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        var lecturers = (await ReadyRecipientsAsync(x.ProjectId, ct)).Where(r => r.Role == ProjectRoles.Lecturer).ToList();
        if (lecturers.Count == 0) throw new InvalidOperationException("Project lecturer is not synchronized yet.");
        foreach (var r in lecturers.Where(r => r.UserId != x.ActorUserId)) Notify(r.UserId, e.Type, "Có yêu cầu duyệt dự án", ProjectLink(x.ProjectId));
    }
    public async Task HandleAsync(EventEnvelope<ReviewDecided> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        foreach (var r in (await ReadyRecipientsAsync(x.ProjectId, ct)).Where(r => r.Role is ProjectRoles.Leader or ProjectRoles.Member).Where(r => r.UserId != x.ActorUserId)) Notify(r.UserId, e.Type, x.Decision == ReviewDecisions.Approved ? "Dự án đã được duyệt" : "Dự án cần chỉnh sửa", ProjectLink(x.ProjectId));
    }
    public async Task HandleAsync(EventEnvelope<CommentAdded> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        foreach (var r in (await ReadyRecipientsAsync(x.ProjectId, ct)).Where(r => r.Role is ProjectRoles.Leader or ProjectRoles.Member).Where(r => r.UserId != x.ActorUserId)) Notify(r.UserId, e.Type, $"Bình luận mới: {x.DiagramTitle}", $"/diagrams/{x.DiagramId}");
    }
    public async Task HandleAsync(EventEnvelope<RenderCompleted> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        // Ngoại lệ BR-34 được Minh chốt: kết quả render gửi cho chính người yêu cầu.
        Notify(x.RequestedBy, e.Type, $"Render hoàn tất: {x.Title}", $"/diagrams/{x.DiagramId}");
    }
    public async Task HandleAsync(EventEnvelope<RenderFailed> e, CancellationToken ct)
    {
        await ValidateAsync(e.Payload, ct);
        var x = e.Payload;
        Notify(x.RequestedBy, e.Type, $"Render thất bại: {x.Title}", $"/diagrams/{x.DiagramId}");
    }
}
