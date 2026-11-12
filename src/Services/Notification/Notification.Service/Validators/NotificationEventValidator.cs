using FluentValidation;
using UMLForge.Contracts.Events;
namespace UMLForge.Notification.Service.Validators;
public sealed class NotificationEventValidator : AbstractValidator<object>
{
    public NotificationEventValidator()
    {
        RuleFor(x => x).Custom((payload, context) =>
        {
            Guid[] ids = payload switch
            {
                ProjectCreated x => [x.ProjectId, x.OwnerId, x.ActorUserId],
                InvitationSent x => [x.InvitationId, x.ProjectId, x.InviteeId, x.ActorUserId],
                MemberAdded x => [x.ProjectId, x.UserId, x.ActorUserId],
                MemberRemoved x => [x.ProjectId, x.UserId, x.ActorUserId],
                LecturerAssigned x => x.PreviousLecturerId.HasValue ? [x.ProjectId, x.LecturerId, x.ActorUserId, x.PreviousLecturerId.Value] : [x.ProjectId, x.LecturerId, x.ActorUserId],
                ProjectDeleted x => [x.ProjectId, x.ActorUserId],
                ReviewSubmitted x => [x.ReviewId, x.ProjectId, x.ActorUserId],
                ReviewDecided x => [x.ReviewId, x.ProjectId, x.ActorUserId],
                CommentAdded x => [x.CommentId, x.DiagramId, x.ProjectId, x.ActorUserId],
                RenderCompleted x => [x.JobId, x.DiagramId, x.ProjectId, x.RequestedBy],
                RenderFailed x => [x.JobId, x.DiagramId, x.ProjectId, x.RequestedBy],
                _ => []
            };
            if (ids.Length == 0 || ids.Any(id => id == Guid.Empty)) context.AddFailure("Ids", "Event IDs must be nonempty.");
            string? name = payload switch
            {
                ProjectCreated x => x.Name,
                InvitationSent x => x.ProjectName,
                MemberAdded x => x.ProjectName,
                LecturerAssigned x => x.ProjectName,
                CommentAdded x => x.DiagramTitle,
                RenderCompleted x => x.Title,
                RenderFailed x => x.Title,
                _ => null
            };
            if (payload is ProjectCreated or InvitationSent or MemberAdded or LecturerAssigned or CommentAdded or RenderCompleted or RenderFailed)
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200) context.AddFailure("Title", "Event title must contain 1–200 characters.");
            if (payload is MemberAdded member && member.Role is not (ProjectRoles.Leader or ProjectRoles.Member)) context.AddFailure("Role", "Invalid project member role.");
            if (payload is ReviewDecided review && review.Decision is not (ReviewDecisions.Approved or ReviewDecisions.ChangesRequested)) context.AddFailure("Decision", "Invalid review decision.");
            if (payload is RenderCompleted done && (done.Version < 1 || done.DurationMs < 0)) context.AddFailure("Version", "Invalid render result.");
            if (payload is RenderFailed failed && failed.Version < 1) context.AddFailure("Version", "Invalid render version.");
        });
    }
}
