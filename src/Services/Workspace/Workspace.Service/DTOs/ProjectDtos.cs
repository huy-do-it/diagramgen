namespace Workspace.Service.DTOs;

public record CreateProjectRequest(string Name, string? Description);

public record UpdateProjectRequest(string Name, string? Description);

public record ProjectSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid? LecturerId,
    int MemberCount,
    DateTime CreatedAt);

public record ProjectMemberResponse(
    Guid UserId,
    string Role,
    DateTime JoinedAt);

public record ProjectDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    Guid? LecturerId,
    DateTime CreatedAt,
    byte[] RowVersion,
    IReadOnlyList<ProjectMemberResponse> Members);
