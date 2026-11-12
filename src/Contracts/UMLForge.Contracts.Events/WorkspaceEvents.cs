namespace UMLForge.Contracts.Events;

// Stream: workspace.events — producer: workspace
// ProjectCreated ngầm định chủ project là thành viên Leader.
public sealed record ProjectCreated(Guid ProjectId, string Name, Guid OwnerId, Guid ActorUserId);

public sealed record InvitationSent(
    Guid InvitationId, Guid ProjectId, string ProjectName, Guid InviteeId, Guid ActorUserId);

public sealed record MemberAdded(
    Guid ProjectId, string ProjectName, Guid UserId, string Role, Guid ActorUserId);

public sealed record MemberRemoved(Guid ProjectId, Guid UserId, Guid ActorUserId);

public sealed record LecturerAssigned(
    Guid ProjectId, string ProjectName, Guid LecturerId, Guid? PreviousLecturerId, Guid ActorUserId);

public sealed record ProjectDeleted(Guid ProjectId, Guid ActorUserId);
